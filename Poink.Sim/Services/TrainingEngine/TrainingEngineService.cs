using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Orchestrates one episode: Data Provider for ticks, Feature Assembler to encode each decision
/// point, the network for a forward pass, Action Assembler's decoded action applied through the
/// existing ITrader (position/P&amp;L accounting, not NN-specific -- reused like ITickRepository),
/// gated by DecisionMask. Reward is plain equity drift, summed inline -- no separate reward-policy
/// abstraction, per the plan to resolve learning/reward directly in this loop.
///
/// Not built yet: gradient-based learning (weights never update, so running more than one episode
/// wouldn't change anything -- this only runs a single rollout), and Volume/StopLoss/TakeProfit
/// outputs (RunEpisode throws if any are enabled -- CapitalFraction is fixed at 1.0, market orders
/// only). Trader State's Outcomes/PositionLevel/Direction history lists are always empty (padded
/// with zeros by FeatureAssembler) since trade-history semantics for those aren't defined yet.
/// </summary>
internal sealed class TrainingEngineService
{
	private readonly DataProvider _dataProvider;
	private readonly FeatureAssembler _featureAssembler;
	private readonly ActionAssembler _actionAssembler;
	private readonly NetworkAssembler _networkAssembler;
	private readonly ITraderFactory _traderFactory;
	private readonly ILogger<TrainingEngineService> _logger;

	public TrainingEngineService(
		DataProvider dataProvider,
		FeatureAssembler featureAssembler,
		ActionAssembler actionAssembler,
		NetworkAssembler networkAssembler,
		ITraderFactory traderFactory,
		ILogger<TrainingEngineService> logger
	)
	{
		_dataProvider = dataProvider;
		_featureAssembler = featureAssembler;
		_actionAssembler = actionAssembler;
		_networkAssembler = networkAssembler;
		_traderFactory = traderFactory;
		_logger = logger;
	}

	public EpisodeResult RunEpisode(TrainingOptionsModel options, Random random)
	{
		if (options.Output.Actions.IncludeVolumeOutput || options.Output.Actions.IncludeStopLossOutput || options.Output.Actions.IncludeTakeProfitOutput)
			throw new NotSupportedException("Volume/StopLoss/TakeProfit outputs aren't wired to trade execution yet -- disable them until they are.");

		var ticks = _dataProvider.GetEpisodeWindow(options.Data, options.Training.EpisodeTiming, options.BarWidthSeconds, random);
		if (ticks.Count == 0)
		{
			_logger.LogWarning("TrainingEngine: episode window for {Table} came back empty, skipping episode", options.Data.Table);
			return EpisodeResult.Empty;
		}

		var actionSpace = _actionAssembler.Assemble(options.Output);
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
		var totalReward = 0.0;
		MlpNetwork? network = null;

		for (var i = 0; i < ticks.Count; i++)
		{
			var tick = ticks[i];
			ticksSoFar.Add(tick);
			var bid = tick.Bid;
			var ask = tick.Ask;

			trader.Advance(bid, ask, tick.Timestamp);

			// Sized off the first tick's own feature vector -- input size depends on how many
			// TxFold timeframes/XGrid signals are enabled, not a fixed constant.
			network ??= _networkAssembler.Assemble(
				options.Network,
				_featureAssembler
					.Flatten(
						_featureAssembler.Assemble(
							options.Features,
							ticksSoFar,
							tick.Timestamp,
							episodeStart,
							BuildTraderStateSnapshot(trader, bid, ask)
						)
					)
					.Values.Length,
				actionSpace.TotalOutputWidth,
				random
			);

			if (!decisionMask.ShouldDecide(tick.Timestamp))
				continue;

			var equityBefore = trader.Snapshot(bid, ask).Equity;

			var vector = _featureAssembler.Flatten(
				_featureAssembler.Assemble(options.Features, ticksSoFar, tick.Timestamp, episodeStart, BuildTraderStateSnapshot(trader, bid, ask))
			);
			var rawOutputs = network.Forward(vector.Values);
			var action = actionSpace.DecodeDirection(rawOutputs[..actionSpace.DirectionOutputWidth], random);
			var execution = trader.Apply(new SimulationDecision(action, CapitalFraction: 1.0), bid, ask, tick.Timestamp);

			var reward = trader.Snapshot(bid, ask).Equity - equityBefore;
			totalReward += reward;
			steps.Add(new EpisodeStep(tick.Timestamp, action, execution, reward));
		}

		var lastTick = ticks[^1];
		return new EpisodeResult(steps, trader.Snapshot(lastTick.Bid, lastTick.Ask), trader.TradeLog, totalReward);
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
