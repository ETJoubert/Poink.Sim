using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Components.Pages.Training.Components.TrainedModelsPanel
{
	/// <summary>
	/// The Models side panel's UI: save the current model, browse/load/delete saved models, and save, load
	/// and delete the training results kept for the selected model. It only raises callbacks; the page
	/// owns the run state those actions read and replace.
	/// </summary>
	public partial class TrainedModelsPanel : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<TrainedModelSummary> Models { get; set; } = [];

		[Parameter, EditorRequired]
		public IReadOnlyList<TrainingHistorySummary> History { get; set; } = [];

		[Parameter]
		public Guid? SelectedModelId { get; set; }

		[Parameter]
		public bool CanSaveModel { get; set; }

		[Parameter]
		public string? SaveModelBlockedReason { get; set; }

		[Parameter]
		public bool CanSaveResults { get; set; }

		[Parameter]
		public string? SaveResultsBlockedReason { get; set; }

		[Parameter]
		public bool CanLoad { get; set; }

		[Parameter]
		public string? LoadBlockedReason { get; set; }

		[Parameter]
		public string? StatusMessage { get; set; }

		[Parameter]
		public bool StatusIsError { get; set; }

		[Parameter]
		public EventCallback<string> OnSaveModel { get; set; }

		[Parameter]
		public EventCallback<Guid> OnLoadModel { get; set; }

		[Parameter]
		public EventCallback<Guid> OnDeleteModel { get; set; }

		[Parameter]
		public EventCallback<Guid> OnSelectModel { get; set; }

		[Parameter]
		public EventCallback<string> OnSaveResults { get; set; }

		[Parameter]
		public EventCallback<Guid> OnLoadResults { get; set; }

		[Parameter]
		public EventCallback<Guid> OnDeleteResults { get; set; }

		private string _modelName = "";
		private string _resultsName = "";

		private string? SelectedModelName => Models.FirstOrDefault(m => m.Id == SelectedModelId)?.Name;

		private bool OverwritesExistingModel => Models.Any(m => m.Name == _modelName.Trim());

		private bool SaveModelEnabled => CanSaveModel && !string.IsNullOrWhiteSpace(_modelName);

		private string SaveModelTitle =>
			SaveModelBlockedReason is { } reason && !CanSaveModel ? reason
			: string.IsNullOrWhiteSpace(_modelName) ? "Enter a name to save under"
			: OverwritesExistingModel ? "Replaces the saved model of this name with the current options and weights; its history is kept"
			: "Saves the current options and weights as a new model";

		private string SaveResultsTitle =>
			SaveResultsBlockedReason is { } reason && !CanSaveResults ? reason : "Saves the Run tab's episode metrics and stats under the selected model";

		private string LoadModelTitle =>
			LoadBlockedReason is { } reason && !CanLoad ? reason : "Restores this model's options and weights, ready to train further";

		private string LoadResultsTitle =>
			LoadBlockedReason is { } reason && !CanLoad ? reason : "Shows these saved results on the Run tab until the next run starts";

		private Task SaveModelClicked() => OnSaveModel.InvokeAsync(_modelName);

		private Task SaveResultsClicked() => OnSaveResults.InvokeAsync(_resultsName);

		private Task DeleteModelClicked(ModelRow row) => OnDeleteModel.InvokeAsync(row.Id);

		private Task DeleteResultsClicked(HistoryRow row) => OnDeleteResults.InvokeAsync(row.Id);

		// DataTable<TItem> reflects over public properties, so the summaries are flattened to display values here.
		private sealed record ModelRow(Guid Id, string Name, int Episodes, long Weights, DateTime Saved);

		private sealed record HistoryRow(Guid Id, string Name, int Episodes, double RecentAvgReward, double BestReward, double WorstReward, DateTime Saved);

		private List<ModelRow> ModelRows =>
			Models.Select(m => new ModelRow(m.Id, m.Name, m.EpisodesTrained, m.WeightCount, m.UpdatedAt ?? m.CreatedAt)).ToList();

		private List<HistoryRow> HistoryRows =>
			History
				.Select(h => new HistoryRow(h.Id, h.Name, h.EpisodesCompleted, h.RecentAverageReward, h.BestReward, h.WorstReward, h.CreatedAt))
				.ToList();
	}
}
