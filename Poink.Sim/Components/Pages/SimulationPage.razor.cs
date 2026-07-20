using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Pages
{
    public partial class SimulationPage : ComponentBase, IDisposable
    {
        [Inject] private SimulationEngine Service { get; set; } = default!;
        [Inject] private ITickRepository TickRepository { get; set; } = default!;
        [Inject] private TickDataStore TickDataStore { get; set; } = default!;

        // ── Setup form state ──

        private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
        private bool _loadingTables;
        private bool _tableListFailed;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private bool _aggregate = true;
        private string _barMinutesText = "1";
        private string _brainName = "";
        private double _startingCash = 10_000;
        private int _traderCount = 1;
        private string? _loadError;

        private string SelectedTable
        {
            get => _table;
            set
            {
                _table = value;
                var meta = _availableTables.FirstOrDefault(t => t.Table == value);
                if (meta.MaxTime is not null)
                {
                    _to = meta.MaxTime.Value;
                    _from = meta.MinTime is not null && meta.MaxTime.Value - meta.MinTime.Value < TimeSpan.FromDays(1)
                        ? meta.MinTime.Value
                        : meta.MaxTime.Value.AddDays(-1);
                }
            }
        }

        private (string Table, long Rows, DateTime? MinTime, DateTime? MaxTime) SelectedTableMeta =>
            _availableTables.FirstOrDefault(t => t.Table == _table);

        // Bounds for the DateRangeSlider -- the selected table's known span when
        // one's picked, else a generous fallback so the slider still renders for
        // Load Dummy Data (which uses _from/_to too, just without a real table).
        private DateTime SliderMin => SelectedTableMeta.MinTime ?? DateTime.UtcNow.AddDays(-30);
        private DateTime SliderMax => SelectedTableMeta.MaxTime ?? DateTime.UtcNow;

        private bool IsDataReady => !string.IsNullOrWhiteSpace(_table) && TickDataStore.IsLoadedFor(_table, _from, _to);

        private List<string> BrainNames => Service.BrainNames.ToList();

        // ── Live state (mirrors the service) ──

        private SimulationSnapshot? _snapshot;
        private bool _isLoading;
        private string? _error;
        private bool _hideChart;
        private int? _selectedTraderId;
        private LogTab _activeLogTab;

        private enum LogTab { Decisions, Positions, Traders }

        // Falls back to the first trader whenever the selected id doesn't match
        // (no snapshot yet, or a fresh load/reset restarted ids at 1) -- no explicit
        // reset needed across load/reset/unload.
        private TraderSnapshot? SelectedTrader =>
            _snapshot is null
                ? null
                : _snapshot.Traders.FirstOrDefault(t => t.Id == _selectedTraderId) ?? _snapshot.Traders.FirstOrDefault();

        private void SelectTrader(int id) => _selectedTraderId = id;

        private int ProgressPercent =>
            _snapshot is null || _snapshot.TotalCount <= 0
                ? 0
                : (int)Math.Round(_snapshot.CursorIndex * 100.0 / _snapshot.TotalCount);

        // ── Lifecycle ──

        // Deliberately synchronous and DB-free: initial render must never wait on
        // Postgres. RefreshTickTableStats() rescans every ticks_* table with a
        // full-scan COUNT(*)/MIN/MAX and upserts tick_table_stats -- slow, and was
        // blocking first paint twice (once for the prerender pass, once for the
        // interactive circuit) when this ran eagerly here. The table list now only
        // loads when the user clicks "LOAD DATA" below (or recounts via "RECOUNT"),
        // except that TickRepository is a singleton, so if another page already did
        // either this run, its in-memory CachedTables is reused here for free.
        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;
            TickDataStore.OnChanged += HandleServiceChanged;
            ApplyServiceState();

            if (BrainNames.Count > 0)
                _brainName = BrainNames[0];

            if (TickRepository.HasCachedTables)
            {
                _availableTables = TickRepository.CachedTables.ToList();
                if (_availableTables.Count > 0 && string.IsNullOrWhiteSpace(_table))
                    SelectedTable = _availableTables[0].Table;
            }
        }

        // Fetches and caches the actual tick rows for the selected table+range
        // (TickDataStore, shared with TrainPage) -- separate from picking a table,
        // and from LoadSimulation() below, which now just consumes whatever's
        // already loaded here instead of re-reading Postgres itself.
        private void LoadData()
        {
            _loadError = null;
            if (string.IsNullOrWhiteSpace(_table)) { _loadError = "pick a symbol"; return; }
            if (_to <= _from) { _loadError = "To must be after From"; return; }

            if (!TickDataStore.TryLoad(_table, _from, _to))
                _loadError = "a load is already in progress";
        }

        // Fast: reads the tick_table_stats registry (small table, no scanning).
        private Task LoadTables() => FetchTables(() => TickRepository.ListTickTables());

        // Slow: rescans every ticks_* table (COUNT(*)/MIN/MAX) and rebuilds the
        // registry -- an explicit action so it never happens as a side effect of
        // just wanting the already-known table list.
        private Task RecountTables() => FetchTables(() => TickRepository.RefreshTickTableStats());

        private async Task FetchTables(Func<List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>> fetch)
        {
            _loadingTables = true;
            _tableListFailed = false;
            StateHasChanged();

            try
            {
                _availableTables = await Task.Run(fetch);
                if (_availableTables.Count > 0 && string.IsNullOrWhiteSpace(_table))
                    SelectedTable = _availableTables[0].Table;
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

        private void HandleServiceChanged() => _ = InvokeAsync(() =>
        {
            ApplyServiceState();
            StateHasChanged();
        });

        private void ApplyServiceState()
        {
            _snapshot = Service.Snapshot;
            _isLoading = Service.IsLoading;
            _error = Service.Error;
        }

        public void Dispose()
        {
            Service.OnChanged -= HandleServiceChanged;
            TickDataStore.OnChanged -= HandleServiceChanged;
        }

        // ── Setup actions ──

        private void LoadSimulation()
        {
            _loadError = null;

            if (string.IsNullOrWhiteSpace(_table)) { _loadError = "pick a symbol"; return; }
            if (_to <= _from) { _loadError = "To must be after From"; return; }
            if (string.IsNullOrWhiteSpace(_brainName)) { _loadError = "pick a brain"; return; }
            if (_startingCash <= 0) { _loadError = "starting cash must be positive"; return; }
            if (!int.TryParse(_barMinutesText, out var barMinutes) || barMinutes < 1) barMinutes = 1;
            if (_traderCount < 1) _traderCount = 1;

            var ticks = TickDataStore.GetLoaded(_table, _from, _to);
            if (ticks is null) { _loadError = "load the data first"; return; }

            var config = new SimulationConfig(
                _table, _from, _to, _aggregate, TimeSpan.FromMinutes(barMinutes), _brainName, _startingCash, _traderCount);

            if (!Service.TryLoad(config, ticks))
                _loadError = "a load is already in progress";
        }

        // Skips Postgres entirely (synthetic random-walk ticks) so the playback UI
        // can be exercised without a running database -- no symbol/date-range
        // validation, since neither is actually read from disk in this path.
        private void LoadDummySimulation()
        {
            _loadError = null;

            if (string.IsNullOrWhiteSpace(_brainName)) { _loadError = "pick a brain"; return; }
            if (_startingCash <= 0) { _loadError = "starting cash must be positive"; return; }
            if (!int.TryParse(_barMinutesText, out var barMinutes) || barMinutes < 1) barMinutes = 1;
            if (_traderCount < 1) _traderCount = 1;

            var to = _to > _from ? _to : DateTime.UtcNow;
            var from = _to > _from ? _from : to.AddHours(-4);
            var table = string.IsNullOrWhiteSpace(_table) ? "DUMMY" : _table;

            var config = new SimulationConfig(
                table, from, to, _aggregate, TimeSpan.FromMinutes(barMinutes), _brainName, _startingCash, _traderCount);

            if (!Service.TryLoadDummy(config))
                _loadError = "a load is already in progress";
        }

        // ── Playback actions ──

        private void TogglePlayPause()
        {
            if (_snapshot?.IsPlaying == true) Service.Pause();
            else Service.Play();
        }

        private void StepOnce() => Service.Step();

        private void SetSpeed(double multiplier) => Service.SetSpeed(multiplier);

        // Max: uncapped backend throughput, chart/tick table still render.
        private void SetMax()
        {
            Service.SetMaxSpeed();
            _hideChart = false;
        }

        // Turbo: same uncapped throughput as Max, but skips chart/tick-table
        // rendering entirely -- only the kv-row (Trader stats) and the
        // trade/decision log stay visible while it runs.
        private void SetTurbo()
        {
            Service.SetMaxSpeed();
            _hideChart = true;
        }

        private void ToggleHideChart() => _hideChart = !_hideChart;

        private void ResetSim() => Service.Reset();

        private void UnloadSim() => Service.Unload();

        // ── Rendering helpers ──

        private static List<CTraderBarRow> ToBarRows(IReadOnlyList<HlocAvdtBar> bars) =>
            bars.Select(b => new CTraderBarRow(
                b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance)).ToList();

        private static List<double> ToMidPrices(IReadOnlyList<Tick> ticks) =>
            ticks.Select(t => (t.Bid + t.Ask) / 2.0).ToList();

        private static string StateClass(SimulationSnapshot s) =>
            s.IsFinished ? "sim-state-finished" : s.IsPlaying ? "sim-state-playing" : "sim-state-paused";

        private static string PnlClass(double v) => v switch
        {
            > 0 => "sim-pnl-positive",
            < 0 => "sim-pnl-negative",
            _ => "",
        };

        private static string Money(double v) => v.ToString("N2");
        private static string PriceStr(double? v) => v is null ? "—" : v.Value.ToString("F5");

        private sealed record TickRow(string Time, string Bid, string Ask, string Mid);

        private List<object> TickRows =>
            _snapshot is null
                ? new List<object>()
                : _snapshot.RecentTicks
                    .Select(object (t) => new TickRow(
                        t.Timestamp.ToString("HH:mm:ss.fff"), t.Bid.ToString("F5"), t.Ask.ToString("F5"),
                        ((t.Bid + t.Ask) / 2).ToString("F5")))
                    .Reverse()
                    .ToList();

        // Plain doubles (not pre-formatted strings) so the copied DataTable's own
        // N2 formatting applies -- CellClass below colors Realized/Unrealized by sign.
        private sealed record TraderCardItem(
            int Id, string Trader, double Cash, double Realized, double Unrealized, double Equity, string Position, int Trades);

        private List<TraderCardItem> TraderItems =>
            _snapshot is null
                ? new List<TraderCardItem>()
                : _snapshot.Traders
                    .Select(t => new TraderCardItem(
                        t.Id, t.Label, t.Capital.Cash, t.Capital.RealizedPnl, t.Capital.UnrealizedPnl,
                        t.Capital.Equity, t.Position is null ? "flat" : t.Position.Value.Direction.ToString(), t.TradeCount))
                    .ToList();

        // Returns DataTable's own scoped color classes (its CSS is isolated to that
        // component, so SimulationPage's sim-pnl-* classes wouldn't apply here).
        private static string TraderCellClass(TraderCardItem item, string propName) => propName switch
        {
            nameof(TraderCardItem.Realized) => DataTablePnlClass(item.Realized),
            nameof(TraderCardItem.Unrealized) => DataTablePnlClass(item.Unrealized),
            _ => "",
        };

        private static string DataTablePnlClass(double v) => v switch
        {
            > 0 => "data-table-value-positive",
            < 0 => "data-table-value-negative",
            _ => "",
        };

        private sealed record LogRow(string Time, string Action, string Price, string Detail);

        private List<object> LogRows =>
            SelectedTrader is not { } trader
                ? new List<object>()
                : trader.RecentLog
                    .Select(object (l) => new LogRow(l.TimestampUtc.ToString("HH:mm:ss"), l.Action, l.Price.ToString("F5"), l.Detail ?? ""))
                    .Reverse()
                    .ToList();

        // v1 (Trader.cs) only ever holds one open Position at a time, so this is
        // 0-or-1 rows today; the table shape is what lets multi-position support
        // slot in later as "more rows" instead of a UI rework.
        private sealed record OpenPositionRow(string Direction, string Size, string EntryPrice, string EntryTime, string UnrealizedPnl);

        private List<object> OpenPositionRows =>
            SelectedTrader?.Position is not { } pos
                ? new List<object>()
                : new List<object>
                {
                    new OpenPositionRow(
                        pos.Direction.ToString(), pos.Size.ToString("F4"), pos.EntryPrice.ToString("F5"),
                        pos.EntryTime.ToString("HH:mm:ss"),
                        _snapshot?.CurrentPrice is { } price ? pos.UnrealizedPnl(price).ToString("F2") : "—"),
                };
    }
}
