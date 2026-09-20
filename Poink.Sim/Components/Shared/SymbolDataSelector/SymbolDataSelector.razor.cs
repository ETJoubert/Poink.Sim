using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Shared.SymbolDataSelector;

/// <summary>
/// Symbol search + date-range slider for picking historic tick data -- unlike TickDataSource (which
/// lists ticks_&lt;symbol&gt; tables directly), this searches the Symbol catalog and resolves the
/// backing table from the match, so callers select a symbol rather than a table name.
/// </summary>
public partial class SymbolDataSelector : ComponentBase
{
	[Inject]
	private ITickRepository TickRepository { get; set; } = default!;

	[Inject]
	private ISymbolService SymbolService { get; set; } = default!;

	[Inject]
	private ILogger<SymbolDataSelector> Logger { get; set; } = default!;

	[Parameter, EditorRequired]
	public string Table { get; set; } = "";

	[Parameter]
	public EventCallback<string> TableChanged { get; set; }

	[Parameter, EditorRequired]
	public DateTime From { get; set; }

	[Parameter]
	public EventCallback<DateTime> FromChanged { get; set; }

	[Parameter, EditorRequired]
	public DateTime To { get; set; }

	[Parameter]
	public EventCallback<DateTime> ToChanged { get; set; }

	[Parameter]
	public bool Disabled { get; set; }

	/// <summary>Fires whenever the resolved symbol changes, so a host can show its catalog details without re-deriving them from the table name.</summary>
	[Parameter]
	public EventCallback<SymbolModel?> SelectedSymbolChanged { get; set; }

	private const int MaxDropdownResults = 40;

	private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
	private IReadOnlyList<SymbolModel> _symbols = [];
	private string _searchText = "";
	private string? _selectedSymbolName;
	private bool _dropdownOpen;
	private bool _loadingTables;
	private bool _tableListFailed;

	private sealed record SymbolOption(string SymbolName, string Table, long Rows, DateTime? MinTime, DateTime? MaxTime, SymbolModel Symbol);

	// Goes symbol -> table via TickTableNaming.ForSymbol, the same function that created the tables --
	// re-parsing a table name back to a symbol can't round-trip once names like "#USNDAQ100" get stripped to alnum-only.
	private IReadOnlyList<SymbolOption> AllOptions
	{
		get
		{
			var tablesByName = _availableTables
				.GroupBy(t => t.Table, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

			var options = new List<SymbolOption>();
			foreach (var symbol in _symbols)
			{
				var liveTable = TickTableNaming.ForSymbol(symbol.SymbolName, isLive: true);
				var demoTable = TickTableNaming.ForSymbol(symbol.SymbolName, isLive: false);

				if (!tablesByName.TryGetValue(liveTable, out var entry) && !tablesByName.TryGetValue(demoTable, out entry))
					continue;

				options.Add(new SymbolOption(symbol.SymbolName, entry.Table, entry.Rows, entry.MinTime, entry.MaxTime, symbol));
			}

			return options.OrderBy(o => o.SymbolName, StringComparer.OrdinalIgnoreCase).ToList();
		}
	}

	private IReadOnlyList<SymbolOption> FilteredOptions =>
		(
			string.IsNullOrWhiteSpace(_searchText)
				? AllOptions
				: AllOptions.Where(o =>
					o.SymbolName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
					|| o.Symbol.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
				)
		)
			.Take(MaxDropdownResults)
			.ToList();

	private SymbolOption? SelectedOption => AllOptions.FirstOrDefault(o => o.SymbolName == _selectedSymbolName);

	private DateTime SliderMin => SelectedOption?.MinTime ?? DateTime.UtcNow.AddDays(-30);
	private DateTime SliderMax => SelectedOption?.MaxTime ?? DateTime.UtcNow;

	protected override void OnInitialized()
	{
		if (TickRepository.HasCachedTables)
			_availableTables = TickRepository.CachedTables.ToList();
		else
			_ = LoadTables();

		_ = LoadSymbols();
	}

	// Resolves a Table the host already had set (e.g. restored state) once the symbol catalog --
	// needed to compute AllOptions -- has actually loaded.
	private void ResolvePreselectedTable()
	{
		if (string.IsNullOrWhiteSpace(Table) || _selectedSymbolName is not null)
			return;

		var match = AllOptions.FirstOrDefault(o => string.Equals(o.Table, Table, StringComparison.OrdinalIgnoreCase));
		if (match is null)
			return;

		_selectedSymbolName = match.SymbolName;
		_searchText = match.SymbolName;
	}

	private Task LoadTables() => FetchTables(() => TickRepository.ListTickTables());

	private Task RecountTables() => FetchTables(() => TickRepository.RefreshTickTableStats());

	private async Task FetchTables(Func<List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>> fetch)
	{
		_loadingTables = true;
		_tableListFailed = false;
		StateHasChanged();

		try
		{
			_availableTables = await Task.Run(fetch);
			ResolvePreselectedTable();
		}
		catch (Exception ex)
		{
			_tableListFailed = true;
			Logger.LogWarning(ex, "Failed to list ticks_* tables for symbol selection");
		}
		finally
		{
			_loadingTables = false;
		}
		StateHasChanged();
	}

	// The Symbol catalog drives matching now (see AllOptions), so nothing is selectable until this loads.
	private async Task LoadSymbols()
	{
		try
		{
			_symbols = await SymbolService.ListAsync();
			ResolvePreselectedTable();
			StateHasChanged();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "Failed to load the Symbol catalog -- symbol search will not find any matches");
		}
	}

	private void OpenDropdown() => _dropdownOpen = true;

	private void CloseDropdown() => _dropdownOpen = false;

	private void OnSearchInput(ChangeEventArgs e)
	{
		_searchText = e.Value?.ToString() ?? "";
		_dropdownOpen = true;
	}

	// Snaps From/To to the newly selected symbol's own table range, matching TickDataSource's behavior.
	private async Task SelectOption(SymbolOption option)
	{
		_selectedSymbolName = option.SymbolName;
		_searchText = option.SymbolName;
		_dropdownOpen = false;

		await TableChanged.InvokeAsync(option.Table);
		await SelectedSymbolChanged.InvokeAsync(option.Symbol);

		if (option.MaxTime is null)
			return;

		var to = option.MaxTime.Value;
		var from =
			option.MinTime is not null && option.MaxTime.Value - option.MinTime.Value < TimeSpan.FromDays(1)
				? option.MinTime.Value
				: option.MaxTime.Value.AddDays(-1);
		await FromChanged.InvokeAsync(from);
		await ToChanged.InvokeAsync(to);
	}
}
