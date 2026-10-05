using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation page's symbol tab: lists every ticks_&lt;symbol&gt; table in the database and picks the single
	/// table the simulation replays, flagging a pick that differs from the running simulation as waiting on SET.
	/// </summary>
	public partial class SimulationSymbolPanel : ComponentBase
	{
		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Inject]
		private ILogger<SimulationSymbolPanel> Logger { get; set; } = default!;

		[Parameter, EditorRequired]
		public string SelectedTable { get; set; } = "";

		[Parameter]
		public EventCallback<string> SelectedTableChanged { get; set; }

		/// <summary>The table the running simulation replays, or null when none is loaded.</summary>
		[Parameter]
		public string? LoadedTable { get; set; }

		[Parameter]
		public bool Disabled { get; set; }

		private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
		private string _filter = "";
		private bool _loadingTables;
		private bool _tableListFailed;

		private bool SelectedAwaitsLoad =>
			LoadedTable is not null && !string.IsNullOrWhiteSpace(SelectedTable) && SelectedTable != LoadedTable;

		private IEnumerable<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> UnselectedTables =>
			_availableTables.Where(t =>
				t.Table != SelectedTable
				&& (string.IsNullOrWhiteSpace(_filter) || t.Table.Contains(_filter.Trim(), StringComparison.OrdinalIgnoreCase))
			);

		protected override void OnInitialized()
		{
			if (TickRepository.HasCachedTables)
				_availableTables = TickRepository.CachedTables.ToList();
			else
				_ = FetchTables(() => TickRepository.ListTickTables());
		}

		private Task RecountTables()
		{
			Logger.LogInformation("Recounting tick tables for the Simulation symbol panel");
			return FetchTables(() => TickRepository.RefreshTickTableStats());
		}

		private async Task FetchTables(Func<List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>> fetch)
		{
			_loadingTables = true;
			_tableListFailed = false;
			StateHasChanged();

			try
			{
				_availableTables = await Task.Run(fetch);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Failed to list tick tables for the Simulation symbol panel");
				_tableListFailed = true;
			}
			finally
			{
				_loadingTables = false;
				StateHasChanged();
			}
		}

		private (string Table, long Rows, DateTime? MinTime, DateTime? MaxTime) Meta(string table) =>
			_availableTables.FirstOrDefault(t => t.Table == table);

		private static string RangeLabel(DateTime? minTime, DateTime? maxTime) =>
			minTime is { } min && maxTime is { } max ? $"{min:yyyy-MM-dd} → {max:yyyy-MM-dd}" : "no range recorded";

		private Task SelectTable(string table) => SelectedTableChanged.InvokeAsync(table);
	}
}
