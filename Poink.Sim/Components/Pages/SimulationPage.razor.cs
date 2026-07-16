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

        // ── Setup form state ──

        private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
        private bool _loadingTables = true;
        private bool _tableListFailed;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private bool _aggregate = true;
        private string _barMinutesText = "1";
        private string _brainName = "";
        private double _startingCash = 10_000;
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

        private List<string> BrainNames => Service.BrainNames.ToList();

        // ── Live state (mirrors the service) ──

        private SimulationSnapshot? _snapshot;
        private bool _isLoading;
        private string? _error;
        private bool _hideChart;

        private int ProgressPercent =>
            _snapshot is null || _snapshot.TotalCount <= 0
                ? 0
                : (int)Math.Round(_snapshot.CursorIndex * 100.0 / _snapshot.TotalCount);

        // ── Lifecycle ──

        protected override async Task OnInitializedAsync()
        {
            Service.OnChanged += HandleServiceChanged;
            ApplyServiceState();

            try
            {
                _availableTables = await Task.Run(() => TickRepository.ListTickTables());
                if (_availableTables.Count > 0)
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

            if (BrainNames.Count > 0)
                _brainName = BrainNames[0];
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

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        // ── Setup actions ──

        private void LoadSimulation()
        {
            _loadError = null;

            if (string.IsNullOrWhiteSpace(_table)) { _loadError = "pick a symbol"; return; }
            if (_to <= _from) { _loadError = "To must be after From"; return; }
            if (string.IsNullOrWhiteSpace(_brainName)) { _loadError = "pick a brain"; return; }
            if (_startingCash <= 0) { _loadError = "starting cash must be positive"; return; }
            if (!int.TryParse(_barMinutesText, out var barMinutes) || barMinutes < 1) barMinutes = 1;

            var config = new SimulationConfig(
                _table, _from, _to, _aggregate, TimeSpan.FromMinutes(barMinutes), _brainName, _startingCash);

            if (!Service.TryLoad(config))
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

            var to = _to > _from ? _to : DateTime.UtcNow;
            var from = _to > _from ? _from : to.AddHours(-4);
            var table = string.IsNullOrWhiteSpace(_table) ? "DUMMY" : _table;

            var config = new SimulationConfig(
                table, from, to, _aggregate, TimeSpan.FromMinutes(barMinutes), _brainName, _startingCash);

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

        private sealed record LogRow(string Time, string Action, string Price, string Detail);

        private List<object> LogRows =>
            _snapshot is null
                ? new List<object>()
                : _snapshot.RecentLog
                    .Select(object (l) => new LogRow(l.TimestampUtc.ToString("HH:mm:ss"), l.Action, l.Price.ToString("F5"), l.Detail ?? ""))
                    .Reverse()
                    .ToList();

        // v1 (Trader.cs) only ever holds one open Position at a time, so this is
        // 0-or-1 rows today; the table shape is what lets multi-position support
        // slot in later as "more rows" instead of a UI rework.
        private sealed record OpenPositionRow(string Direction, string Size, string EntryPrice, string EntryTime, string UnrealizedPnl);

        private List<object> OpenPositionRows =>
            _snapshot?.Position is not { } pos
                ? new List<object>()
                : new List<object>
                {
                    new OpenPositionRow(
                        pos.Direction.ToString(), pos.Size.ToString("F4"), pos.EntryPrice.ToString("F5"),
                        pos.EntryTime.ToString("HH:mm:ss"),
                        _snapshot.CurrentPrice is { } price ? pos.UnrealizedPnl(price).ToString("F2") : "—"),
                };
    }
}
