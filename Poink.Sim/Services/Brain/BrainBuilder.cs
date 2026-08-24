
using System.Linq;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Brain;

/// <summary>
/// Constructs an <see cref="IDescribableBrain"/> from a <see cref="BrainRecipe"/>. Pure
/// construction only -- no persistence, no registry mutation.
/// </summary>
internal sealed class BrainBuilder : IBrainBuilder
{
    public IDescribableBrain Build(string name, BrainRecipe recipe)
    {
        var parts = recipe.EncoderKinds.Select(k => BrainCatalog.CreateEncoder(k, recipe.EncoderSizes)).ToArray();
        var actionOutputHeads = (recipe.ActionOutputKinds ?? [])
            .Select(k => BrainCatalog.CreateActionOutputHead(k, recipe.ActionOutputEncodingKind, recipe.DecisionDecoding, recipe.DecisionTemperature)).ToList();
        var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;

        if (recipe.ArchitectureKind == ArchitectureKind.Neat)
            return NeatPolicyBrain.Create(
                name, recipe.NeatHyperparameters ?? new NeatHyperparameters(), actionOutputHeads, directions,
                recipe.DecisionDecoding, recipe.DecisionTemperature, recipe.EnabledMaskRules, parts);

        var architecture = BrainCatalog.CreateArchitecture(recipe.ArchitectureKind, recipe.HiddenSizes);
        return ComposedPolicyBrain.Create(
            name, architecture, actionOutputHeads, directions, recipe.DecisionDecoding, recipe.DecisionTemperature, recipe.CnnKernelSweepSource,
            recipe.EnabledMaskRules, parts);
    }
}
