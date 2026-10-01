using System;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Gates whether the network is even asked to decide -- once per closed bar, once per configured
/// interval, or on every tick. Action-legality masking lives in the Output tab, not here.
/// </summary>
internal sealed class DecisionMask
{
	private readonly DecisionMaskResolutionKind _resolution;
	private readonly double _resolutionSeconds;

	private DateTime? _lastDecisionTime;

	public DecisionMask(DecisionMaskingOptionsModel options)
	{
		if (options.DecisionMaskResolution == DecisionMaskResolutionKind.OnCellWidthChange)
			throw new NotSupportedException("Cell-width decision resolution isn't supported -- use Once per bar, Every X seconds, or Every tick.");

		_resolution = options.DecisionMaskResolution;
		_resolutionSeconds = options.DecisionMaskResolutionSeconds;
	}

	public bool ShouldDecide(DateTime asOf, bool barJustClosed)
	{
		var isCheckpoint = _resolution switch
		{
			DecisionMaskResolutionKind.PerBar => barJustClosed,
			DecisionMaskResolutionKind.PerTick => true,
			_ => _lastDecisionTime is not { } last || (asOf - last).TotalSeconds >= _resolutionSeconds,
		};
		if (!isCheckpoint)
			return false;

		_lastDecisionTime = asOf;
		return true;
	}
}
