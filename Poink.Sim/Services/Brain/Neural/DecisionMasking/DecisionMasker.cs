using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Brain.Neural;

// Gates whether a brain is even asked to decide -- opposite of DirectionActionMasker, which
// restricts choices after a decision has already been made. Inert with zero rules enabled.
internal sealed class DecisionMasker
{
	private readonly DecisionMaskResolutionKind _resolution;
	private readonly TimeSpan _everySecondsInterval;
	private readonly PriceCellTracker? _cellTracker;
	private readonly IReadOnlyList<IDecisionMaskRule> _rules;
	private DateTime? _lastCheckedAt;

	public DecisionMasker(
		DecisionMaskResolutionKind resolution,
		double everySecondsInterval,
		double cellWidth,
		IReadOnlyList<IDecisionMaskRule> rules
	)
	{
		_resolution = resolution;
		_everySecondsInterval = TimeSpan.FromSeconds(Math.Max(0.001, everySecondsInterval));
		_cellTracker = resolution == DecisionMaskResolutionKind.OnCellWidthChange
			? new PriceCellTracker(cellWidth)
			: null;
		_rules = rules;
	}

	public bool HasActiveDecisionMask => _rules.Count > 0;

	public bool ShouldDecide(Tick tick, bool barCompletedThisTick, SimulationObservation observation)
	{
		bool isCheckpoint = _resolution switch
		{
			DecisionMaskResolutionKind.PerBar => barCompletedThisTick,
			DecisionMaskResolutionKind.PerTick => true,
			DecisionMaskResolutionKind.EverySeconds =>
				_lastCheckedAt is null || tick.Timestamp - _lastCheckedAt.Value >= _everySecondsInterval,
			DecisionMaskResolutionKind.OnCellWidthChange => _cellTracker!.Feed(tick) is not null,
			_ => throw new ArgumentOutOfRangeException(nameof(tick), _resolution, null),
		};

		if (!isCheckpoint)
			return false;

		_lastCheckedAt = tick.Timestamp;
		return _rules.All(rule => rule.IsSatisfied(observation));
	}
}
