using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Runs training: builds one network up front, then runs EpisodeCount episodes against it, handing
/// each episode's recorded decisions to PolicyGradientLearner afterward. Each episode steps Data
/// Provider's ticks through Feature Assembler, the network, Action Assembler's decoded action, and
/// the existing ITrader (position/P&amp;L/commission accounting -- reused like ITickRepository, not
/// NN-specific), gated by DecisionMask. Reward is each decision's equity drift until the next one.
///
/// Not built yet: Volume/StopLoss/TakeProfit outputs (RunEpisode throws if any are enabled --
/// CapitalFraction is fixed at 1.0, market orders only).
/// </summary>
internal sealed class TrainingEngineService
{
	private readonly DataProvider _dataProvider;
	private readonly FeatureAssembler _featureAssembler;
	private readonly ActionAssembler _actionAssembler;
	private readonly NetworkAssembler _networkAssembler;
	private readonly PolicyGradientLearner _learner;
	private readonly ITraderFactory _traderFactory;
	private readonly ILogger<TrainingEngineService> _logger;

	public TrainingEngineService(
		DataProvider dataProvider,
		FeatureAssembler featureAssembler,
		ActionAssembler actionAssembler,
		NetworkAssembler networkAssembler,
		PolicyGradientLearner learner,
		ITraderFactory traderFactory,
		ILogger<TrainingEngineService> logger
	)
	{
		_dataProvider = dataProvider;
		_featureAssembler = featureAssembler;
		_actionAssembler = actionAssembler;
		_networkAssembler = networkAssembler;
		_learner = learner;
		_traderFactory = traderFactory;
		_logger = logger;
	}

	public MlpNetwork BuildNetwork(TrainingOptionsModel options, ActionSpace actionSpace, Random random) =>
		_networkAssembler.Assemble(
			options.Network,
			FeatureAssembler.ComputeInputSize(options.Features),
			actionSpace.TotalOutputWidth,
			random
		);

	// Runs EpisodeCount episodes against one network, learning between them -- unlike calling
	// RunEpisode standalone with a fresh network each time, which would just repeat random rollouts.
	public async Task<IReadOnlyList<EpisodeResult>> Train(TrainingOptionsModel options, Random random)
	{
		var actionSpace = _actionAssembler.Assemble(options.Output);
		var network = BuildNetwork(options, actionSpace, random);

		var episodeCount = options.Training.EpisodeTiming.EpisodeCount;
		var results = new List<EpisodeResult>(episodeCount);
		for (var i = 0; i < episodeCount; i++)
		{
			var progress = episodeCount > 1 ? (double)i / (episodeCount - 1) : 1.0;
			results.Add(await RunEpisode(options, network, actionSpace, random, progress));
		}

		return results;
	}

	// Reward for a decision is the equity drift while it's in force, resolved once the next decision
	// arrives -- not a same-tick delta around Apply, which would miss all holding-period P&L.
	public async Task<EpisodeResult> RunEpisode(
		TrainingOptionsModel options,
		MlpNetwork network,
		ActionSpace actionSpace,
		Random random,
		double progress = 0.0,
		Action<LiveEpisodeSnapshot>? onDecision = null,
		Action<LiveTickSnapshot>? onTick = null,
		Func<TimeSpan>? tickDelay = null,
		Func<bool>? isActionPreviewWanted = null,
		Func<bool>? isPaused = null,
		CancellationToken cancellationToken = default
	)
	{
		if (options.Output.Actions.IncludeVolumeOutput || options.Output.Actions.IncludeStopLossOutput || options.Output.Actions.IncludeTakeProfitOutput)
			throw new NotSupportedException("Volume/StopLoss/TakeProfit outputs aren't wired to trade execution yet -- disable them until they are.");
		PolicyGradientLearner.EnsureSupportedStrategy(options.Training.Learning.TrainingStrategyName);
		var positionControl = options.Training.TraderSetup.EffectiveControl;
		if (positionControl.Validate() is { } controlError)
			throw new InvalidOperationException($"Position Control is invalid: {controlError}");

		var ticks = _dataProvider.GetEpisodeWindow(options.Data, options.Training.EpisodeTiming, options.BarWidthSeconds, random);
		if (ticks.Count == 0)
		{
			_logger.LogWarning("TrainingEngine: episode window for {Table} came back empty, skipping episode", options.Data.Table);
			return EpisodeResult.Empty;
		}

		var trader = _traderFactory.Create(
			options.Training.TraderSetup.StartingCash,
			new TraderSettings(
				Leverage: options.Training.TraderSetup.Leverage,
				StopOutLevelPercent: options.Training.TraderSetup.StopOutLevelPercent,
				CommissionModel: new PercentOfNotionalCommission(options.Training.TraderSetup.CommissionRatePercent)
			)
		);
		var decisionMask = new DecisionMask(options.Training.DecisionMasking);

		var episodeStart = ticks[0].Timestamp;
		var ticksSoFar = new List<Tick>(ticks.Count);
		var steps = new List<EpisodeStep>();
		var traces = new List<DecisionTrace>();
		var totalReward = 0.0;
		var barBuilder = new LiveBarBuilder(options.BarWidthSeconds);

		var previousDecisionEquity = trader.Snapshot(ticks[0].Bid, ticks[0].Ask).Equity;
		var reportedTradeCount = 0;
		var owedDelay = TimeSpan.Zero;
		IReadOnlyList<Trade> closedTradesCopy = [];
		var copiedTradeCount = 0;

		IReadOnlyList<Trade> ClosedTradesCopiedOnChange()
		{
			if (trader.TradeLog.Count != copiedTradeCount)
			{
				closedTradesCopy = [.. trader.TradeLog];
				copiedTradeCount = closedTradesCopy.Count;
			}
			return closedTradesCopy;
		}
		var previewClock = Stopwatch.StartNew();
		var previewInterval = MinPreviewInterval;

		// Sleeps are batched because Task.Delay can't honour sub-timer-resolution waits tick by tick.
		async Task PaceAsync()
		{
			owedDelay += tickDelay?.Invoke() ?? TimeSpan.Zero;
			if (owedDelay < MinPacingSleep)
				return;
			await Task.Delay(owedDelay, cancellationToken);
			owedDelay = TimeSpan.Zero;
		}

		foreach (var tick in ticks)
		{
			ticksSoFar.Add(tick);
			var bid = tick.Bid;
			var ask = tick.Ask;
			var (currentBar, justClosedBar) = barBuilder.Feed(tick);

			trader.Advance(bid, ask, tick.Timestamp);

			var shouldDecide = decisionMask.ShouldDecide(tick.Timestamp);

			if (onTick is not null)
			{
				(float[] Masked, float[] PreMask)? preview = null;
				var previewDue = !shouldDecide && previewClock.Elapsed >= previewInterval && (isActionPreviewWanted?.Invoke() ?? true);
				if (previewDue)
				{
					var previewStarted = Stopwatch.GetTimestamp();
					preview = PreviewDirectionOutputs(options, network, actionSpace, trader, ticksSoFar, episodeStart, tick);
					previewInterval = Max(MinPreviewInterval, Stopwatch.GetElapsedTime(previewStarted) * PreviewOverheadFactor);
					previewClock.Restart();
				}
				var tradesChanged = trader.TradeLog.Count != reportedTradeCount;
				reportedTradeCount = trader.TradeLog.Count;
				onTick(
					new LiveTickSnapshot(
						tick.Timestamp,
						bid,
						ask,
						currentBar,
						justClosedBar,
						trader.Snapshot(bid, ask),
						trader.CurrentPosition,
						tradesChanged ? [.. trader.TradeLog] : null,
						preview?.Masked,
						preview?.PreMask,
						ticksSoFar.Count,
						ticks.Count
					)
				);
			}

			if (!shouldDecide)
			{
				await PaceAsync();
				continue;
			}

			while (isPaused is not null && isPaused())
				await Task.Delay(150, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			if (steps.Count > 0)
			{
				var reward = trader.Snapshot(bid, ask).Equity - previousDecisionEquity;
				totalReward += reward;
				steps[^1] = steps[^1] with { EquityDelta = reward };
				traces[^1] = traces[^1] with { Reward = reward };
			}

			var featureModel = _featureAssembler.Assemble(
				options.Features,
				ticksSoFar,
				tick.Timestamp,
				episodeStart,
				BuildTraderStateSnapshot(trader, bid, ask, options.Features.TraderState.PreviousTradesCount)
			);
			var vector = _featureAssembler.Flatten(featureModel);
			var (forwardOutputs, activations) = network.ForwardWithActivations(vector.Values);
			var rawOutputs = MaskIllegalDirections(forwardOutputs, actionSpace, trader.CurrentPosition, options.Output.ActionMasking);
			var actionIndex = ChooseActionIndex(rawOutputs, actionSpace, random);
			var action = actionSpace.Directions[actionIndex];
			var execution = trader.Apply(
				PositionControlHelper.BuildDecision(action, positionControl, trader.Cash, options.Training.TraderSetup.Leverage, options.Data.PipSize, bid, ask),
				bid,
				ask,
				tick.Timestamp
			);

			var capitalAfter = trader.Snapshot(bid, ask);
			previousDecisionEquity = capitalAfter.Equity;
			steps.Add(new EpisodeStep(tick.Timestamp, action, execution, EquityDelta: 0));
			traces.Add(new DecisionTrace(activations, rawOutputs, actionIndex, Reward: 0));

			onDecision?.Invoke(
				new LiveEpisodeSnapshot(
					episodeStart,
					tick.Timestamp,
					bid,
					ask,
					currentBar,
					justClosedBar,
					featureModel,
					action,
					rawOutputs[..actionSpace.DirectionOutputWidth],
					capitalAfter,
					ClosedTradesCopiedOnChange(),
					trader.CurrentPosition,
					forwardOutputs[..actionSpace.DirectionOutputWidth],
					ticksSoFar.Count,
					ticks.Count
				)
			);

			await PaceAsync();
		}

		if (steps.Count > 0)
		{
			steps.RemoveAt(steps.Count - 1);
			traces.RemoveAt(traces.Count - 1);
		}

		_learner.Learn(network, actionSpace, traces, options.Training.Learning, progress);

		var lastTick = ticks[^1];
		return new EpisodeResult(steps, trader.Snapshot(lastTick.Bid, lastTick.Ask), trader.TradeLog, totalReward);
	}

	private static readonly TimeSpan MinPreviewInterval = TimeSpan.FromMilliseconds(50);

	// Spacing previews at this multiple of their own cost caps their share of training time near 1/factor.
	private const int PreviewOverheadFactor = 10;

	private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;

	// A forward pass over every tick's features is too costly at high playback speeds, so the Live tab's
	// action preview refreshes on a throttle that backs off as the pass gets slower.
	private (float[] Masked, float[] PreMask) PreviewDirectionOutputs(
		TrainingOptionsModel options,
		MlpNetwork network,
		ActionSpace actionSpace,
		ITrader trader,
		IReadOnlyList<Tick> ticksSoFar,
		DateTime episodeStart,
		Tick tick
	)
	{
		var featureModel = _featureAssembler.Assemble(
			options.Features,
			ticksSoFar,
			tick.Timestamp,
			episodeStart,
			BuildTraderStateSnapshot(trader, tick.Bid, tick.Ask, options.Features.TraderState.PreviousTradesCount)
		);
		var outputs = network.Forward(_featureAssembler.Flatten(featureModel).Values);
		var masked = MaskIllegalDirections(outputs, actionSpace, trader.CurrentPosition, options.Output.ActionMasking);
		return (masked[..actionSpace.DirectionOutputWidth], outputs[..actionSpace.DirectionOutputWidth]);
	}

	private static readonly TimeSpan MinPacingSleep = TimeSpan.FromMilliseconds(15);

	private const float IllegalDirectionPenalty = -1e9f;

	// Applied to the logits themselves (not just filtered out after sampling) so an illegal
	// direction is also excluded from the softmax PolicyGradientLearner trains on.
	private static float[] MaskIllegalDirections(
		float[] rawOutputs,
		ActionSpace actionSpace,
		Position? position,
		ActionMaskingOptionsModel masking
	)
	{
		var masked = (float[])rawOutputs.Clone();
		for (var i = 0; i < actionSpace.DirectionOutputWidth; i++)
			if (!IsLegalDirection(actionSpace.Directions[i], position, masking))
				masked[i] = IllegalDirectionPenalty;
		return masked;
	}

	private static bool IsLegalDirection(SimulationAction action, Position? position, ActionMaskingOptionsModel masking)
	{
		if (masking.PositionRequiredForClose && action == SimulationAction.Close && position is null)
			return false;
		if (masking.FlatRequiredForEntry && action is SimulationAction.EnterLong or SimulationAction.EnterShort && position is not null)
			return false;
		return true;
	}

	// Same action DecodeDirection would pick, but the gradient also needs to know which output
	// slot it came from -- IReadOnlyList has no IndexOf, so this just walks the short list.
	private static int ChooseActionIndex(float[] rawOutputs, ActionSpace actionSpace, Random random)
	{
		var action = actionSpace.DecodeDirection(rawOutputs[..actionSpace.DirectionOutputWidth], random);
		var directions = actionSpace.Directions;
		for (var i = 0; i < directions.Count; i++)
			if (directions[i] == action)
				return i;
		return -1;
	}

	private static TraderStateSnapshot BuildTraderStateSnapshot(ITrader trader, double bid, double ask, int previousTradesCount)
	{
		var snapshot = trader.Snapshot(bid, ask);
		var log = trader.TradeLog;
		var previousTradesRealized = new List<double>(Math.Min(previousTradesCount, log.Count));
		for (var i = log.Count - 1; i >= 0 && previousTradesRealized.Count < previousTradesCount; i--)
			previousTradesRealized.Add(RiskHelper.PercentOfBalance(log[i].RealizedPnl, trader.Cash) ?? 0);
		return new(
			RiskHelper.PercentOfBalance(snapshot.UnrealizedPnl, trader.Cash) ?? 0,
			RiskHelper.PercentOfBalance(snapshot.RealizedPnl, trader.Cash) ?? 0,
			previousTradesRealized
		);
	}
}
