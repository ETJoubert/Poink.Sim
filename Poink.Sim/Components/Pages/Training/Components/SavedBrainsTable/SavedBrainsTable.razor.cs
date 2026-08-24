using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Browsable history of saved brain snapshots -- shared between TrainPage
    // (continue training from a prior snapshot) and SimulationPage (pick a
    // trained brain to run), since both just need "list what's saved, load
    // one's weights into its live brain, or delete it" with no other
    // page-specific behavior. See RlTrainingService.LoadSavedBrain's doc
    // comment: loading here is also how a saved brain becomes what
    // SimulationPage's brain picker will use, since both pages share the
    // same live singleton brains by name.
    public partial class SavedBrainsTable : ComponentBase
    {
        [Parameter, EditorRequired] public IReadOnlyList<NeuralWeightsSummary> SavedBrains { get; set; } = [];

        [Parameter] public EventCallback<NeuralWeightsSummary> OnUse { get; set; }
        [Parameter] public EventCallback<NeuralWeightsSummary> OnDelete { get; set; }

        [Parameter] public string Title { get; set; } = "Saved brains";
        [Parameter] public string UseTitle { get; set; } = "Loads these weights into the brain's live weights";
        [Parameter] public bool StartExpanded { get; set; } = true;

        private bool _expanded;

        protected override void OnInitialized() => _expanded = StartExpanded;

        private void HandleUse(SavedBrainRow row) =>
            OnUse.InvokeAsync(SavedBrains.FirstOrDefault(b => b.Id == row.Id));

        private void HandleDelete(SavedBrainRow row) =>
            OnDelete.InvokeAsync(SavedBrains.FirstOrDefault(b => b.Id == row.Id));

        // Flattens TrainedModelSummary into a DataTable-friendly shape -- the
        // generic DataTable<TItem> reflects over public properties directly
        // (see its View/Card rendering), so Parameters needs to already be a
        // display string rather than the nested ModelTrainingParameters object.
        // Structure (not "Brain") -- this is the composed architecture/name
        // string (e.g. "MLP_L64-L32-L16_dBW-dVPR-dTPR-dAS"), a shape
        // identifier, not a reference to a Brain entity. Status/TrainedAt are
        // derived from TrainedModelSummary.TrainedAt -- null means this
        // snapshot was saved before any training episode actually completed
        // for it, distinct from SavedAt.
        private sealed record SavedBrainRow(
            Guid Id, string Name, string Structure, string Strategy, string RewardPolicy, string Dataset,
            long WeightCount, string Parameters, double AvgReward, string Status, DateTime? TrainedAt, DateTime SavedAt);

        private List<SavedBrainRow> Rows =>
            SavedBrains.Select(b => new SavedBrainRow(
                b.Id, b.Name, b.BrainName, b.StrategyName, b.RewardPolicyName, b.Dataset, b.WeightCount, b.Parameters.ToString(), b.AvgReward,
                b.TrainedAt is not null ? "Trained" : "Untrained", b.TrainedAt, b.SavedAt)).ToList();
    }
}
