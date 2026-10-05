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
	/// The Simulation page's symbol tab: lists every ticks_&lt;symbol&gt; table in the database and keeps an ordered
	/// selection whose first entry is the traded symbol and the rest watched symbols, flagging any entry that differs
	/// from the running simulation as waiting on SET.
	/// </summary>
	public partial class SimulationSymbolPanel : ComponentBase
	{
		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Inject]
		private ILogger<SimulationSymbolPanel> Logger { get; set; } = default!;

		/// <summary>Selected tables in order; the first is the traded symbol.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<string> SelectedTables { get; set; } = [];

		[Parameter]
		public EventCallback<IReadOnlyList<string>> SelectedTablesChanged { get; set; }

		/// <summary>The running simulation's tables, traded first, or null when none is loaded.</summary>
		[Parameter]
		public IReadOnlyList<string>? LoadedTables { get; set; }

		[Parameter]
		public bool Disabled { get; set; }

		private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
		private string _filter = "";
		private bool _loadingTables;
		private bool _tableListFailed;

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

		private bool AwaitsLoad(string table, bool isTraded) =>
			LoadedTables is { Count: > 0 } loaded
			&& (isTraded ? loaded[0] != table : !loaded.Skip(1).Contains(table));

		private (string Table, long Rows, DateTime? MinTime, DateTime? MaxTime) Meta(string table) =>
			_availableTables.FirstOrDefault(t => t.Table == table);

		private static string RangeLabel(DateTime? minTime, DateTime? maxTime) =>
			minTime is { } min && maxTime is { } max ? $"{min:yyyy-MM-dd} → {max:yyyy-MM-dd}" : "no range recorded";

		private Task AddTable(string table) => ApplySelection(SelectedTables.Append(table));

		private Task RemoveTable(string table) => ApplySelection(SelectedTables.Where(t => t != table));

		private Task MakeTraded(string table) => ApplySelection(SelectedTables.Where(t => t != table).Prepend(table));

		private Task ClearSelection() => ApplySelection([]);

		private Task ApplySelection(IEnumerable<string> tables) =>
			SelectedTablesChanged.InvokeAsync(tables.Distinct().ToList());
	}
}
