using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.Symbols;
using Poink.Sim.SignalStudy;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// Builds signal datasets -- the rule engine's signals stored once per second for one fixed profile of settings -- and
	/// lists the ones stored so far. The analysis of what follows each signal comes later.
	/// </summary>
	public partial class SignalStudyPage : ComponentBase, IDisposable
	{
		[Inject]
		private ISignalDatasetBuilder Builder { get; set; } = default!;

		[Inject]
		private ISignalDatasetService Datasets { get; set; } = default!;

		[Inject]
		private ISymbolTableResolver SymbolTableResolver { get; set; } = default!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		[Inject]
		private ISignalAnalysisService Analysis { get; set; } = default!;

		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Inject]
		private ILogger<SignalStudyPage> Logger { get; set; } = default!;

		private string _table = string.Empty;
		private DateTime _from = DateTime.UtcNow.Date.AddDays(-7);
		private DateTime _to = DateTime.UtcNow.Date;
		private string _name = string.Empty;
		private double? _pipSize;
		private SignalProfile _profile = new();
		private IReadOnlyList<SignalDatasetInfo> _datasets = Array.Empty<SignalDatasetInfo>();
		private string? _datasetsError;
		private bool _wasBuilding;

		private long? _analysisDatasetId;
		private SignalAnalysisOptions _analysisOptions = new();
		private string _horizonsText = "5, 15, 60";
		private SignalAnalysisResult? _analysisResult;
		private string? _analysisError;
		private bool _analysisRunning;
		private int _horizonIndex;
		private CancellationTokenSource? _analysisCancellation;

		private IEnumerable<SignalDatasetInfo> CompleteDatasets => _datasets.Where(d => d.Status == SignalDatasetStatus.Complete);

		private SignalDatasetInfo? SelectedAnalysisDataset => CompleteDatasets.FirstOrDefault(d => d.Id == _analysisDatasetId);

		private string HorizonLabel(SignalAnalysisResult result) =>
			_horizonIndex < result.Options.HorizonMinutes.Count ? $"{result.Options.HorizonMinutes[_horizonIndex]}m" : "";

		private string DefaultName => $"{_table} {_from:yyyy-MM-dd} → {_to:yyyy-MM-dd} {BarSizeOptions.Label(_profile.BarSizeSeconds)}";

		private string? ValidationMessage =>
			string.IsNullOrWhiteSpace(_table) ? "Choose a symbol table"
			: _to <= _from ? "The range must end after it starts"
			: _pipSize is not > 0 ? "Pip size is unknown -- enter it"
			: null;

		protected override async Task OnInitializedAsync()
		{
			Builder.OnChanged += HandleBuilderChanged;
			_wasBuilding = Builder.IsBuilding;
			await ReloadDatasetsAsync();
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!firstRender)
				return;
			await LoadFromSimulationAsync();
			StateHasChanged();
		}

		private void HandleBuilderChanged() =>
			_ = InvokeAsync(async () =>
			{
				bool startedOrFinished = _wasBuilding != Builder.IsBuilding;
				_wasBuilding = Builder.IsBuilding;
				if (startedOrFinished)
					await ReloadDatasetsAsync();
				StateHasChanged();
			});

		private async Task LoadFromSimulationAsync()
		{
			try
			{
				if (await LocalStorage.GetItemAsync<SimulationPageSettings>(SimulationPageSettings.StorageKey) is not { } settings)
					return;
				_table = settings.Table;
				_from = settings.From;
				_to = settings.To;
				_profile = _profile with
				{
					BarSizeSeconds = settings.BarSizeSeconds,
					Signals = settings.SignalSettings ?? _profile.Signals,
				};
				await ResolvePipSizeAsync();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Could not read the Simulation page's saved settings");
			}
		}

		private async Task ResolvePipSizeAsync()
		{
			if (string.IsNullOrWhiteSpace(_table))
				return;
			try
			{
				var resolution = await SymbolTableResolver.ResolveAsync(_table);
				if (resolution.Limits is { } limits)
					_pipSize = limits.PipSize;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Could not resolve the pip size for table {Table}", _table);
			}
		}

		private void StartBuild()
		{
			if (ValidationMessage is not null || _pipSize is not { } pipSize)
				return;
			var name = string.IsNullOrWhiteSpace(_name) ? DefaultName : _name.Trim();
			var request = new SignalDatasetBuildRequest(
				name,
				_table,
				DateTime.SpecifyKind(_from, DateTimeKind.Utc),
				DateTime.SpecifyKind(_to, DateTimeKind.Utc),
				pipSize,
				_profile
			);
			if (Builder.TryStart(request))
				_name = string.Empty;
		}

		private async Task ReloadDatasetsAsync()
		{
			try
			{
				_datasets = await Datasets.ListAsync();
				_datasetsError = null;
			}
			catch (Exception ex)
			{
				_datasetsError = $"Could not list datasets: {ex.Message}";
				Logger.LogError(ex, "Could not list signal datasets");
			}
		}

		private async Task DeleteDatasetAsync(SignalDatasetInfo dataset)
		{
			try
			{
				await Datasets.DeleteAsync(dataset.Id);
				_datasetsError = null;
			}
			catch (Exception ex)
			{
				_datasetsError = $"Could not delete {dataset.Name}: {ex.Message}";
				Logger.LogError(ex, "Could not delete signal dataset {Id}", dataset.Id);
			}
			await ReloadDatasetsAsync();
		}

		private void SetPipSize(ChangeEventArgs e) =>
			_pipSize = double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
				? value
				: null;

		private void SetInt(ChangeEventArgs e, int min, int max, Func<int, SignalProfile> apply)
		{
			if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
				_profile = apply(Math.Clamp(value, min, max));
		}

		private void SetDouble(ChangeEventArgs e, Func<double, SignalProfile> apply)
		{
			if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
				_profile = apply(Math.Max(0, value));
		}

		private void SelectForAnalysis(SignalDatasetInfo dataset) => _analysisDatasetId = dataset.Id;

		private void SelectAnalysisDataset(ChangeEventArgs e) =>
			_analysisDatasetId = long.TryParse(e.Value?.ToString(), out var id) ? id : null;

		private void SetHorizons(ChangeEventArgs e)
		{
			_horizonsText = e.Value?.ToString() ?? string.Empty;
			var horizons = _horizonsText
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ? minutes : 0)
				.Where(minutes => minutes > 0)
				.Distinct()
				.OrderBy(minutes => minutes)
				.ToList();
			if (horizons.Count > 0)
				_analysisOptions = _analysisOptions with { HorizonMinutes = horizons };
		}

		private void SetOptionDouble(ChangeEventArgs e, Func<double, SignalAnalysisOptions> apply)
		{
			if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
				_analysisOptions = apply(value);
		}

		private void SetOptionInt(ChangeEventArgs e, int min, Func<int, SignalAnalysisOptions> apply)
		{
			if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
				_analysisOptions = apply(Math.Max(min, value));
		}

		private async Task RunAnalysisAsync()
		{
			if (SelectedAnalysisDataset is not { } dataset)
				return;
			_analysisCancellation = new CancellationTokenSource();
			_analysisRunning = true;
			_analysisError = null;
			try
			{
				_analysisResult = await Analysis.AnalyzeAsync(dataset, _analysisOptions, _analysisCancellation.Token);
				_horizonIndex = Math.Min(_horizonIndex, _analysisResult.Options.HorizonMinutes.Count - 1);
			}
			catch (OperationCanceledException)
			{
				_analysisError = "Analysis cancelled";
			}
			catch (Exception ex)
			{
				_analysisError = $"Analysis failed: {ex.Message}";
				Logger.LogError(ex, "Signal analysis failed for dataset {Id}", dataset.Id);
			}
			finally
			{
				_analysisRunning = false;
				_analysisCancellation.Dispose();
				_analysisCancellation = null;
			}
		}

		private void CancelAnalysis() => _analysisCancellation?.Cancel();

		private async Task ExportCsvAsync()
		{
			if (_analysisResult is not { } result)
				return;
			try
			{
				await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/fileDownload.js");
				var fileName = $"signal-analysis-{result.DatasetId}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
				await module.InvokeVoidAsync("downloadText", fileName, SignalAnalysisCsv.Build(result), "text/csv");
			}
			catch (Exception ex)
			{
				_analysisError = $"Export failed: {ex.Message}";
				Logger.LogError(ex, "Could not export the signal analysis of dataset {Id}", result.DatasetId);
			}
		}

		public void Dispose()
		{
			Builder.OnChanged -= HandleBuilderChanged;
			_analysisCancellation?.Cancel();
		}
	}
}
