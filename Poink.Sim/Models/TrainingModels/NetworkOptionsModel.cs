using System.Collections.Generic;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>
	/// The Network tab's architecture settings -- which of MLP/LSTM/CNN's parameters apply depends
	/// on ArchitectureKind; the others are simply unused rather than validated against it.
	/// </summary>
	internal sealed record NetworkOptionsModel(
		ArchitectureKind ArchitectureKind = ArchitectureKind.Mlp,
		IReadOnlyList<int>? MlpHiddenSizes = null,
		int LstmHiddenSize = 64,
		int LstmNumLayers = 2,
		int CnnKernelSize = 3,
		IReadOnlyList<int>? CnnChannels = null,
		CnnKernelSweepSource CnnKernelSweepSource = CnnKernelSweepSource.BarWindow
	);
}
