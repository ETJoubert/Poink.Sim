using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal readonly record struct FeatureDescriptor(string Label, IFeatureNormalizer Normalizer);

internal interface IObservationEncoder
{
	int FeatureCount { get; }

	int SequenceLength { get; }

	int WarmupBars { get; }

	IReadOnlyList<FeatureDescriptor> Features { get; }

	float[] Encode(SimulationObservation observation);
}
