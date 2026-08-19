
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal interface IBrainService
{
    IReadOnlyList<BrainSummary> Brains { get; }
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

    /// <summary>Builds a live brain from a recipe, registers it, and records the recipe against its name.</summary>
    IPolicyGradientBrain BuildAndRegister(string name, BrainRecipe recipe);

    bool TryBuildDraftBrain(
        string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes,
        IReadOnlyList<ActionOutputKind>? actionOutputKinds = null, IReadOnlyList<SimulationAction>? enabledDirections = null,
        EncoderSizeOptions? encoderSizes = null, DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
        double decisionTemperature = 1.0, CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow);

    bool TryBuildBrain(
        string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes,
        IReadOnlyList<ActionOutputKind>? actionOutputKinds = null, IReadOnlyList<SimulationAction>? enabledDirections = null,
        EncoderSizeOptions? encoderSizes = null, DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
        double decisionTemperature = 1.0, CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow);

    bool CreateNetwork(string name, BrainRecipe recipe);
    bool UpdateNetwork(long id, string name, BrainRecipe recipe);
    bool DeleteNetwork(long id);
    void Refresh();
}
