using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Shared.TickDataSource;

// Symbol picker + date-range slider for a ticks_<symbol> table -- shared by
// SimulationPage and TrainPage so both pages pick historic data the same way,
// with the same RECOUNT-only button flow (no separate "load tables" step;
// the table list fetches itself on init).
public partial class TickDataSource : ComponentBase
{
    [Inject] private ITickRepository TickRepository { get; set; } = default!;

    [Parameter, EditorRequired] public string Table { get; set; } = "";
    [Parameter] public EventCallback<string> TableChanged { get; set; }
    [Parameter, EditorRequired] public DateTime From { get; set; }
    [Parameter] public EventCallback<DateTime> FromChanged { get; set; }
    [Parameter, EditorRequired] public DateTime To { get; set; }
    [Parameter] public EventCallback<DateTime> ToChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }

    private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
    private bool _loadingTables;
    private bool _tableListFailed;

    private (string Table, long Rows, DateTime? MinTime, DateTime? MaxTime) SelectedTableMeta =>
        _availableTables.FirstOrDefault(t => t.Table == Table);

    // Bounds for the DateRangeSlider -- the selected table's known span when
    // one's picked, else a generous fallback so the slider still renders
    // before any table list has loaded.
    private DateTime SliderMin => SelectedTableMeta.MinTime ?? DateTime.UtcNow.AddDays(-30);
    private DateTime SliderMax => SelectedTableMeta.MaxTime ?? DateTime.UtcNow;

    // ITickRepository is a singleton, so if another page already loaded the
    // table list this run, reuse it here for free instead of re-fetching.
    // Otherwise kick off the same fetch RECOUNT triggers manually -- fire-
    // and-forget so first paint isn't blocked on Postgres.
    protected override void OnInitialized()
    {
        if (TickRepository.HasCachedTables)
        {
            _availableTables = TickRepository.CachedTables.ToList();
            _ = SelectDefaultTableIfNeeded();
        }
        else
        {
            _ = LoadTables();
        }
    }

    // Fast: reads the tick_table_stats registry (small table, no scanning).
    private Task LoadTables() => FetchTables(() => TickRepository.ListTickTables());

    // Slow: rescans every ticks_* table (COUNT(*)/MIN/MAX) and rebuilds the
    // registry -- the only remaining manual action, so it never happens as a
    // side effect of just visiting the page.
    private Task RecountTables() => FetchTables(() => TickRepository.RefreshTickTableStats());

    private async Task FetchTables(Func<List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>> fetch)
    {
        _loadingTables = true;
        _tableListFailed = false;
        StateHasChanged();

        try
        {
            _availableTables = await Task.Run(fetch);
            await SelectDefaultTableIfNeeded();
        }
        catch
        {
            _tableListFailed = true;
        }
        finally
        {
            _loadingTables = false;
        }
    }

    private Task SelectDefaultTableIfNeeded() =>
        _availableTables.Count > 0 && string.IsNullOrWhiteSpace(Table)
            ? SelectTable(_availableTables[0].Table)
            : Task.CompletedTask;

    private Task OnTableSelected(ChangeEventArgs e) => SelectTable(e.Value?.ToString() ?? "");



    // Also snaps From/To to the newly-selected table's own range -- same
    // behavior each page had inline before this was extracted.
    private async Task SelectTable(string value)
    {
        await TableChanged.InvokeAsync(value);

        var meta = _availableTables.FirstOrDefault(t => t.Table == value);
        if (meta.MaxTime is null)
            return;

        var to = meta.MaxTime.Value;
        var from = meta.MinTime is not null && meta.MaxTime.Value - meta.MinTime.Value < TimeSpan.FromDays(1)
            ? meta.MinTime.Value
            : meta.MaxTime.Value.AddDays(-1);
        await FromChanged.InvokeAsync(from);
        await ToChanged.InvokeAsync(to);
    }
}
