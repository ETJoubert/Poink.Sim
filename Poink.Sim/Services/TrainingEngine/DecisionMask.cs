using System;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Gates whether the network is even asked to decide -- resolves once per configured interval.
/// Action-legality masking isn't built yet; it'll be added as its own follow-up.
/// </summary>
internal sealed class DecisionMask
{
	private readonly double _resolutionSeconds;

	private DateTime? _lastDecisionTime;

	public DecisionMask(DecisionMaskingOptionsModel options)
	{
		_resolutionSeconds = options.DecisionMaskResolutionSeconds;
	}

	public bool ShouldDecide(DateTime asOf)
	{
		if (_lastDecisionTime is { } last && (asOf - last).TotalSeconds < _resolutionSeconds)
			return false;

		_lastDecisionTime = asOf;
		return true;
	}
}
