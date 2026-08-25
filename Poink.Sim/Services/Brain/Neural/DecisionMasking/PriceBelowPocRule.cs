using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class PriceBelowPocRule : IDecisionMaskRule
{
	private readonly XGridSignalEncoder _encoder;

	public PriceBelowPocRule(XGridSignalEncoder encoder) => _encoder = encoder;

	public bool IsSatisfied(SimulationObservation observation) =>
		_encoder.HasSignal(XGridSignalCatalog.Keys.BelowPoc, observation);
}
