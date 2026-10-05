using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// The Profiler page's left side panel: lists every ticks_&lt;symbol&gt; table in the database and keeps an ordered
	/// selection whose first entry is the primary symbol, the one whose chart set carries the page's drawings.
	/// </summary>
	public partial class ProfilerSymbolPanel : ComponentBase
	{
		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Inject]
		private ILogger<ProfilerSymbolPanel> Logger { get; set; } = default!;

		/// <summary>Selected tables in display order; the first is the primary symbol.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<string> SelectedTables { get; set; } = [];

		[Parameter]
		public EventCallback<IReadOnlyList<string>> SelectedTablesChanged { get; set; }

		/// <summary>Table to theme colour var; each selected symbol is drawn in its colour so this panel is the page's legend.</summary>
		[Parameter]
		public IReadOnlyDictionary<string, string> SymbolColors { get; set; } = new Dictionary<string, string>();

		/// <summary>The table the page last loaded, so a primary picked since then can be flagged as waiting on LOAD.</summary>
		[Parameter]
		public string? LoadedTable { get; set; }

		[Parameter]
		public bool Disabled { get; set; }

		private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
		private string _filter = "";
		private bool _collapsed;
		private bool _loadingTables;
		private bool _tableListFailed;

		private string? PrimaryTable => SelectedTables.Count > 0 ? SelectedTables[0] : null;

		private bool PrimaryAwaitsLoad => PrimaryTable is not null && LoadedTable is not null && PrimaryTable != LoadedTable;

		private IEnumerable<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> UnselectedTables =>
			_availableTables.Where(t =>
				!SelectedTables.Contains(t.Table)
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
			Logger.LogInformation("Recounting tick tables for the Profiler symbol panel");
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
				Logger.LogError(ex, "Failed to list tick tables for the Profiler symbol panel");
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

		private string SymbolColorStyle(string table) =>
			$"--psp-symbol-color: var({ProfilerSymbolColorHelper.ColorVarFor(SymbolColors, table)})";

		private static string RangeLabel(DateTime? minTime, DateTime? maxTime) =>
			minTime is { } min && maxTime is { } max ? $"{min:yyyy-MM-dd} → {max:yyyy-MM-dd}" : "no range recorded";

		private Task AddTable(string table) => ApplySelection(SelectedTables.Append(table));

		private Task RemoveTable(string table) => ApplySelection(SelectedTables.Where(t => t != table));

		private Task MakePrimary(string table) => ApplySelection(SelectedTables.Where(t => t != table).Prepend(table));

		private Task ClearSelection() => ApplySelection([]);

		private Task ApplySelection(IEnumerable<string> tables) =>
			SelectedTablesChanged.InvokeAsync(tables.Distinct().ToList());
	}
}
