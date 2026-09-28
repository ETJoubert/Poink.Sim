using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Runs training: builds one network up front, then runs EpisodeCount episodes against it, handing
/// each episode's recorded decisions to PolicyGradientLearner afterward. Each episode steps Data
/// Provider's ticks through Feature Assembler, the network, Action Assembler's decoded action, and
/// the existing ITrader (position/P&amp;L/commission accounting -- reused like ITickRepository, not
/// NN-specific), gated by DecisionMask. Reward is plain equity drift, summed inline.
///
/// Not built yet: Volume/StopLoss/TakeProfit outputs (RunEpisode throws if any are enabled --
/// CapitalFraction is fixed at 1.0, market orders only). Trader State's Outcomes/PositionLevel/
/// Direction history lists are always empty (padded with zeros by FeatureAssembler) since
/// trade-history semantics for those aren't defined yet.
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

		var results = new List<EpisodeResult>(options.Training.EpisodeTiming.EpisodeCount);
		for (var i = 0; i < options.Training.EpisodeTiming.EpisodeCount; i++)
			results.Add(await RunEpisode(options, network, actionSpace, random));

		return results;
	}

	// decisionDelay/isPaused/cancellationToken back the Live tab's Play/Pause/Speed controls, all
	// re-read every decision (not snapshotted) so changing Speed mid-episode takes effect immediately.
	public async Task<EpisodeResult> RunEpisode(
		TrainingOptionsModel options,
		MlpNetwork network,
		ActionSpace actionSpace,
		Random random,
		Action<LiveEpisodeSnapshot>? onDecision = null,
		Func<TimeSpan>? decisionDelay = null,
		Func<bool>? isPaused = null,
		CancellationToken cancellationToken = default
	)
	{
		if (options.Output.Actions.IncludeVolumeOutput || options.Output.Actions.IncludeStopLossOutput || options.Output.Actions.IncludeTakeProfitOutput)
			throw new NotSupportedException("Volume/StopLoss/TakeProfit outputs aren't wired to trade execution yet -- disable them until they are.");
		PolicyGradientLearner.EnsureSupportedStrategy(options.Training.Learning.TrainingStrategyName);

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

		foreach (var tick in ticks)
		{
			ticksSoFar.Add(tick);
			var bid = tick.Bid;
			var ask = tick.Ask;
			var (currentBar, justClosedBar) = barBuilder.Feed(tick);

			trader.Advance(bid, ask, tick.Timestamp);

			if (!decisionMask.ShouldDecide(tick.Timestamp))
				continue;

			while (isPaused is not null && isPaused())
				await Task.Delay(150, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			var equityBefore = trader.Snapshot(bid, ask).Equity;

			var featureModel = _featureAssembler.Assemble(
				options.Features,
				ticksSoFar,
				tick.Timestamp,
				episodeStart,
				BuildTraderStateSnapshot(trader, bid, ask)
			);
			var vector = _featureAssembler.Flatten(featureModel);
			var (rawOutputs, activations) = network.ForwardWithActivations(vector.Values);
			var actionIndex = ChooseActionIndex(rawOutputs, actionSpace, random);
			var action = actionSpace.Directions[actionIndex];
			var execution = trader.Apply(new SimulationDecision(action, CapitalFraction: 1.0), bid, ask, tick.Timestamp);

			var capitalAfter = trader.Snapshot(bid, ask);
			var reward = capitalAfter.Equity - equityBefore;
			totalReward += reward;
			steps.Add(new EpisodeStep(tick.Timestamp, action, execution, reward));
			traces.Add(new DecisionTrace(activations, rawOutputs, actionIndex, reward));

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
					[.. trader.TradeLog]
				)
			);

			var delay = decisionDelay?.Invoke() ?? TimeSpan.Zero;
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);
		}

		_learner.Learn(network, actionSpace, traces, options.Training.Learning);

		var lastTick = ticks[^1];
		return new EpisodeResult(steps, trader.Snapshot(lastTick.Bid, lastTick.Ask), trader.TradeLog, totalReward);
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

	private static TraderStateSnapshot BuildTraderStateSnapshot(ITrader trader, double bid, double ask)
	{
		var capital = trader.Snapshot(bid, ask);
		var position = trader.CurrentPosition;

		return new TraderStateSnapshot(
			position is { } p ? (p.Direction == PositionDirection.Long ? 1.0 : -1.0) : 0.0,
			position?.EntryPrice ?? 0,
			position?.TicksHeld ?? 0,
			position?.CumulativeDistance ?? 0,
			(bid + ask) / 2.0,
			trader.Cash,
			trader.RealizedPnl,
			capital.UnrealizedPnl,
			capital.Equity,
			trader.MarginUsed,
			capital.MarginLevel ?? 0,
			position is null ? 0 : 1,
			[],
			[],
			[]
		);
	}
}
