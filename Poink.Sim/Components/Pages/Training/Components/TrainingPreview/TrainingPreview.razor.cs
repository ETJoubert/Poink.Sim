using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Training
{
    // Read-only summary of the brain that Start/Step will train, derived
    // straight from its BrainRecipe rather than any panel's in-progress
    // fields -- so it works whether the recipe came from BrainBuilder or was
    // loaded from a saved brain. Sits on TrainPage above the Start button,
    // independent of Brain Builder's own collapsible panel. DataSelector and
    // Training config summaries are a planned follow-up, not this component's
    // concern yet.
    public partial class TrainingPreview : ComponentBase
    {
        [Parameter] public BrainRecipe? Recipe { get; set; }
        [Parameter] public IReadOnlyList<string> DataItems { get; set; } = [];
        [Parameter] public IReadOnlyList<string> TrainingItems { get; set; } = [];

        private IReadOnlyList<TrainingPreviewSourceRow> SourceRows => BuildSourceRows(Recipe);
        private IReadOnlyList<string> StructureItems => BuildStructureItems(Recipe);
        private IReadOnlyList<string> ActionItems => BuildActionItems(Recipe);
        private IReadOnlyList<string> DecodedActionItems => BuildDecodedActionItems(Recipe);

        private static string NormalizationLabel(NormalizationKind kind) => kind switch
        {
            NormalizationKind.Raw => "Raw",
            NormalizationKind.LocalZScore => "Local Z-Score",
            NormalizationKind.GlobalZScore => "Global Z-Score",
            _ => "Normalized",
        };

        // One row per included input set, in the same order as Brain
        // Builder's own input table, always ending with the always-on
        // Trader State.
        private static IReadOnlyList<TrainingPreviewSourceRow> BuildSourceRows(BrainRecipe? recipe)
        {
            var rows = new List<TrainingPreviewSourceRow>();
            if (recipe is null) return rows;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            if (recipe.EncoderKinds.Contains(EncoderKind.BarWindow))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.BarWindow, sizes).FeatureCount;
                rows.Add(new("Bar Window", count.ToString(), NormalizationLabel(sizes.BarWindowNormalization),
                    $"{sizes.BarWindowSize} bars", "—"));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.VolumeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.VolumeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.VolumeProfileNormalization) + (sizes.VolumeProfileSplitUpDown ? " (split)" : "");
                rows.Add(new("Volume Profile", count.ToString(), normalization,
                    $"{sizes.VolumeProfileLookbackBars} bars", sizes.VolumeProfileBucketCount.ToString()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.TimeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.TimeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.TimeProfileNormalization) + (sizes.TimeProfileSplitUpDown ? " (split)" : "");
                rows.Add(new("Time Profile", count.ToString(), normalization,
                    $"{sizes.TimeProfileLookbackBars} bars", sizes.TimeProfileBucketCount.ToString()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.SessionClock))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.SessionClock, sizes).FeatureCount;
                rows.Add(new("Session Clock", count.ToString(), "—", "—", "—"));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.SpreadVolatility))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.SpreadVolatility, sizes).FeatureCount;
                rows.Add(new("Spread + Volatility", count.ToString(), "—", $"{sizes.SpreadVolatilityAtrPeriod} bars", "—"));
            }
            rows.Add(new("Trader State", new TraderStateEncoder().FeatureCount.ToString(), "Raw", "—", "—"));
            return rows;
        }

        // Architecture kind followed by its shape (hidden layers, LSTM
        // size/depth, or CNN kernel/sweep/channels) -- mirrors
        // BrainCatalog.CreateArchitecture's own reading of HiddenSizes.
        private static IReadOnlyList<string> BuildStructureItems(BrainRecipe? recipe)
        {
            if (recipe is null) return [];

            var items = new List<string>();
            var hiddenSizes = recipe.HiddenSizes;
            switch (recipe.ArchitectureKind)
            {
                case ArchitectureKind.Lstm:
                    items.Add("LSTM");
                    items.Add($"Hidden: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 64)}");
                    items.Add($"Layers: {(hiddenSizes.Count > 1 ? hiddenSizes[1] : 2)}");
                    break;
                case ArchitectureKind.Cnn:
                    items.Add("CNN");
                    items.Add($"Sweeps: {(recipe.CnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? "Volume Profile" : "Bar Window")}");
                    items.Add($"Kernel: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 3)}");
                    var channels = hiddenSizes.Count > 1 ? hiddenSizes.Skip(1).ToList() : [16, 32];
                    for (int i = 0; i < channels.Count; i++)
                        items.Add($"C{i + 1}: {channels[i]}");
                    break;
                default:
                    items.Add("MLP");
                    for (int i = 0; i < hiddenSizes.Count; i++)
                        items.Add($"L{i + 1}: {hiddenSizes[i]}");
                    break;
            }
            return items;
        }

        // Every enabled direction plus optional Volume/Stop Loss/Take Profit
        // outputs.
        private static IReadOnlyList<string> BuildActionItems(BrainRecipe? recipe)
        {
            if (recipe is null) return [];

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            var items = new List<string>();
            if (directions.Contains(SimulationAction.Hold)) items.Add("Hold");
            if (directions.Contains(SimulationAction.EnterLong)) items.Add("Enter Long");
            if (directions.Contains(SimulationAction.EnterShort)) items.Add("Enter Short");
            if (directions.Contains(SimulationAction.Close)) items.Add("Close");

            var actionKinds = recipe.ActionOutputKinds ?? [];
            if (actionKinds.Contains(ActionOutputKind.Volume)) items.Add("Volume");
            if (actionKinds.Contains(ActionOutputKind.StopLoss)) items.Add("Stop Loss");
            if (actionKinds.Contains(ActionOutputKind.TakeProfit)) items.Add("Take Profit");
            return items;
        }

        // One line per independently-decoded softmax group, mirroring Brain
        // Builder's own Action Decoding rows.
        private static IReadOnlyList<string> BuildDecodedActionItems(BrainRecipe? recipe)
        {
            if (recipe is null) return [];

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            var decodingLabel = recipe.DecisionDecoding switch
            {
                DecisionDecodingKind.HardMax => "hardmax",
                _ when recipe.DecisionTemperature != 1.0 => $"softmax @ T={recipe.DecisionTemperature:0.##}",
                _ => "softmax",
            };

            var items = new List<string> { $"Direction: {directions.Count}-way {decodingLabel}" };
            var actionKinds = recipe.ActionOutputKinds ?? [];
            if (actionKinds.Contains(ActionOutputKind.Volume))
                items.Add($"Volume: {BrainCatalog.CreateActionOutputHead(ActionOutputKind.Volume, recipe.ActionOutputEncodingKind).OutputWidth}-way {decodingLabel}");
            if (actionKinds.Contains(ActionOutputKind.StopLoss))
                items.Add($"Stop Loss: {BrainCatalog.CreateActionOutputHead(ActionOutputKind.StopLoss, recipe.ActionOutputEncodingKind).OutputWidth}-way {decodingLabel}");
            if (actionKinds.Contains(ActionOutputKind.TakeProfit))
                items.Add($"Take Profit: {BrainCatalog.CreateActionOutputHead(ActionOutputKind.TakeProfit, recipe.ActionOutputEncodingKind).OutputWidth}-way {decodingLabel}");
            return items;
        }
    }

    // One row of the Source/Inputs/Normalization/Window/Bucket columns --
    // Normalization/Window/Bucket are "--" where a source has no such concept
    // (e.g. Session Clock has no window, Bar Window has no bucketing).
    public sealed record TrainingPreviewSourceRow(string Source, string Inputs, string Normalization, string Window, string Bucket);
}
