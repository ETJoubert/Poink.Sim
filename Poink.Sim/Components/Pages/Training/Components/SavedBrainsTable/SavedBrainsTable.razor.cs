using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Repository;

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
        [Parameter, EditorRequired] public IReadOnlyList<SavedBrainSummary> SavedBrains { get; set; } = [];

        [Parameter] public EventCallback<SavedBrainSummary> OnUse { get; set; }
        [Parameter] public EventCallback<SavedBrainSummary> OnDelete { get; set; }

        [Parameter] public string Title { get; set; } = "Saved brains";
        [Parameter] public string UseTitle { get; set; } = "Loads these weights into the brain's live weights";
        [Parameter] public bool StartExpanded { get; set; } = true;

        private bool _expanded;

        protected override void OnInitialized() => _expanded = StartExpanded;

        private void HandleUse(SavedBrainRow row) =>
            OnUse.InvokeAsync(SavedBrains.FirstOrDefault(b => b.Id == row.Id));

        private void HandleDelete(SavedBrainRow row) =>
            OnDelete.InvokeAsync(SavedBrains.FirstOrDefault(b => b.Id == row.Id));

        // Flattens SavedBrainSummary into a DataTable-friendly shape -- the
        // generic DataTable<TItem> reflects over public properties directly
        // (see its View/Card rendering), so Parameters needs to already be a
        // display string rather than the nested BrainTrainingParameters object.
        private sealed record SavedBrainRow(
            long Id, string Name, string Brain, string Strategy, string Dataset,
            long WeightCount, string Parameters, double AvgReward, DateTime SavedAt);

        private List<SavedBrainRow> Rows =>
            SavedBrains.Select(b => new SavedBrainRow(
                b.Id, b.Name, b.BrainName, b.StrategyName, b.Dataset, b.WeightCount, b.Parameters.ToString(), b.AvgReward, b.SavedAt)).ToList();
    }
}
