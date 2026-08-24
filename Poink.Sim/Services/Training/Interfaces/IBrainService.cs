using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal interface IBrainService
{
	IReadOnlyList<NeuralNetworkSummary> Brains { get; }
	IReadOnlyList<string> BrainNames { get; }
	IReadOnlyList<string> DraftBrainNames { get; }
	IReadOnlyList<BrainOption> BrainOptions { get; }
	string? Status { get; }

	event Action? OnChanged;

	/// <summary>Fires with a brain's name whenever an already-registered live instance of it is discarded and rebuilt from a recipe (its weights reset).</summary>
	event Action<string>? BrainReset;

	BrainRecipe? GetRecipe(string brainName);
	IReadOnlyList<NetworkLayerShape> DescribeNetworkShape(string brainName);
	int GetWarmupBars(string brainName);

	bool TryBuildDraftBrain(
		string name,
		IReadOnlyList<EncoderKind> encoderKinds,
		ArchitectureKind architectureKind,
		IReadOnlyList<int> hiddenSizes,
		IReadOnlyList<ActionOutputKind>? actionOutputKinds = null,
		IReadOnlyList<SimulationAction>? enabledDirections = null,
		EncoderSizeOptions? encoderSizes = null,
		DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
		double decisionTemperature = 1.0,
		CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow,
		IReadOnlyList<DirectionMaskRuleKind>? enabledMaskRules = null,
		NeatHyperparameters? neatHyperparameters = null
	);
	public IDescribableBrain AssembleBrain(string name, BrainRecipe recipe);
	bool TryBuildBrain(
		string name,
		IReadOnlyList<EncoderKind> encoderKinds,
		ArchitectureKind architectureKind,
		IReadOnlyList<int> hiddenSizes,
		IReadOnlyList<ActionOutputKind>? actionOutputKinds = null,
		IReadOnlyList<SimulationAction>? enabledDirections = null,
		EncoderSizeOptions? encoderSizes = null,
		DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
		double decisionTemperature = 1.0,
		CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow,
		IReadOnlyList<DirectionMaskRuleKind>? enabledMaskRules = null,
		NeatHyperparameters? neatHyperparameters = null
	);

	bool CreateNetwork(string name, BrainRecipe recipe);
	bool UpdateNetwork(Guid id, string name, BrainRecipe recipe);
	bool DeleteNetwork(Guid id);
	void Refresh();
}
