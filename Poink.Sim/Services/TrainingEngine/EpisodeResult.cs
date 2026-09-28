using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>One decided-and-applied step of an episode -- Execution is null when the decision was Hold or got no-opped by the trader (e.g. zero cash).</summary>
internal readonly record struct EpisodeStep(DateTime TimestampUtc, SimulationAction Action, TradeExecution? Execution, double EquityDelta);

/// <summary>The full outcome of one episode rollout -- Reward is the same equity-drift sum RunEpisode's caller would need for a future learning step.</summary>
internal sealed record EpisodeResult(IReadOnlyList<EpisodeStep> Steps, CapitalState FinalCapital, IReadOnlyList<Trade> ClosedTrades, double Reward)
{
	public static readonly EpisodeResult Empty = new([], default, [], 0);
}
