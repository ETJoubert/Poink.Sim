using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal interface INeatPolicyBrain : IDescribableBrain
{
	NeatPopulation Population { get; }

	int WarmupBars => 0;

	(SimulationDecision Decision, IReadOnlyList<RlOutputFeature> Outputs) DecideWithOutputs(
		SimulationObservation observation
	);

	NeatTopologySnapshot DescribeTopology();
}
