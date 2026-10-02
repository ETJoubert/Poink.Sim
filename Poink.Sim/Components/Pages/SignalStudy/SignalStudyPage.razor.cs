using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
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

		public void Dispose() => Builder.OnChanged -= HandleBuilderChanged;
	}
}
