using System.Collections.Generic;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Brain.Neural;

internal interface IActionOutputHead
{
	ActionOutputKind Kind { get; }

	int OutputWidth { get; }

	(double? Value, Tensor LogProbability, IReadOnlyList<RlOutputFeature> Outputs) Sample(Tensor rawSlice);
}
