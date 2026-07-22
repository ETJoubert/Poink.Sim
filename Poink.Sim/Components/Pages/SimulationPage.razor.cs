using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Training;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Pages
{
    public partial class SimulationPage : ComponentBase, IDisposable
    {
        [Inject] private SimulationEngine Service { get; set; } = default!;

        // RlTrainingService owns the same live ISimulationBrain singletons this
        // page's SimulationEngine drives -- injected only for its brain
        // metadata (BrainOptions/GetActiveSnapshot) to feed the BRAIN tab, not
        // for anything training-related.
        [Inject] private RlTrainingService TrainingService { get; set; } = default!;

        // ── Setup form state ──

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private bool _aggregate = true;
        private string _barMinutesText = "1";
        private string _brainName = "";
        private double _startingCash = 10_000;
        private int _traderCount = 1;
        private string? _loadError;

        private List<string> BrainNames => Service.BrainNames.ToList();

        // ── Live state (mirrors the service) ──

        private SimulationSnapshot? _snapshot;
        private bool _isLoading;
        private string? _error;
        private bool _hideChart;
        private int? _selectedTraderId;
        private LogTab _activeLogTab;

        // Shared "how many of the most recent ticks" control -- drives both the
        // TickVolumeProfileChart panel and the window-start marker on OhlcvChart, so it
        // lives here rather than inside either chart component (see their [Parameter]s).
        private const int MaxTickWindow = 5000; // matches SimulationEngine.MaxRecentTicks
        private int _tickWindowSize = 200;
        private int ClampedTickWindow => Math.Clamp(_tickWindowSize, 2, MaxTickWindow);
        private double TickWindowFillPercent => ClampedTickWindow * 100.0 / MaxTickWindow;

        // Timestamp of the tick that starts the current window -- null once the window
        // covers the whole buffer, so OhlcvChart knows to hide the marker rather than
        // drawing it at the very first bar. Snapped to the same bar-boundary formula
        // SimulationBarAggregator.Feed uses so it lands exactly on a rendered bar's
        // OpenTime -- lightweight-charts' timeToCoordinate only resolves times that
        // exist as an actual point on the chart's time axis (an arbitrary in-between
        // tick timestamp resolves to no coordinate at all, so nothing draws).
        private DateTime? WindowStartUtc(IReadOnlyList<Tick> ticks, TimeSpan barSize)
        {
            if (ticks.Count <= ClampedTickWindow)
                return null;
            var raw = ticks[ticks.Count - ClampedTickWindow].Timestamp;
            return new DateTime(raw.Ticks - (raw.Ticks % barSize.Ticks), raw.Kind);
        }

        private enum LogTab { Decisions, Positions, Traders, Brain, Statistics }

        // The brain actually driving the loaded run (s.Config.BrainName) --
        // deliberately not the Setup form's _brainName, which the user may
        // have since changed to something not in use by this run.
        private bool CurrentBrainIsNeuralNetwork(string brainName) =>
            TrainingService.BrainOptions.FirstOrDefault(b => b.Name == brainName)?.IsNeuralNetwork ?? false;

        // Falls back to the first trader whenever the selected id doesn't match
        // (no snapshot yet, or a fresh load/reset restarted ids at 1) -- no explicit
        // reset needed across load/reset/unload.
        private TraderSnapshot? SelectedTrader =>
            _snapshot is null
                ? null
                : _snapshot.Traders.FirstOrDefault(t => t.Id == _selectedTraderId) ?? _snapshot.Traders.FirstOrDefault();

        private void SelectTrader(int id) => _selectedTraderId = id;

        // ── Lifecycle ──

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;
            ApplyServiceState();

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

        public void Dispose()
        {
            Service.OnChanged -= HandleServiceChanged;
        }

        // ── Setup actions ──

        private void LoadSimulation()
        {
            _loadError = null;

            if (string.IsNullOrWhiteSpace(_table))
            { _loadError = "pick a symbol"; return; }
            if (_to <= _from)
            { _loadError = "To must be after From"; return; }
            if (string.IsNullOrWhiteSpace(_brainName))
            { _loadError = "pick a brain"; return; }
            if (_startingCash <= 0)
            { _loadError = "starting cash must be positive"; return; }
            if (!int.TryParse(_barMinutesText, out var barMinutes) || barMinutes < 1)
                barMinutes = 1;
            if (_traderCount < 1)
                _traderCount = 1;

            var config = new SimulationConfig(
                _table, _from, _to, _aggregate, TimeSpan.FromMinutes(barMinutes), _brainName, _startingCash, _traderCount);

            if (!Service.TryLoad(config))
                _loadError = "a load is already in progress";
        }

        // Skips Postgres entirely (synthetic random-walk ticks) so the playback UI
        // can be exercised without a running database -- no symbol/date-range
        // validation, since neither is actually read from disk in this path.
        private void LoadDummySimulation()
        {
            _loadError = null;

            if (string.IsNullOrWhiteSpace(_brainName))
            { _loadError = "pick a brain"; return; }
            if (_startingCash <= 0)
            { _loadError = "starting cash must be positive"; return; }
            if (!int.TryParse(_barMinutesText, out var barMinutes) || barMinutes < 1)
                barMinutes = 1;
            if (_traderCount < 1)
                _traderCount = 1;

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
            if (_snapshot?.IsPlaying == true)
                Service.Pause();
            else
                Service.Play();
        }

        private void StepOnce() => Service.Step();

        private void SetSpeed(double multiplier) => Service.SetSpeed(multiplier);

        // Bounded speed presets the +/- stepper cycles through -- separate from
        // Max, which is an uncapped mode the engine treats entirely differently
        // (see SimulationEngine.SetMaxSpeed).
        private static readonly double[] Speeds = [0.1, 1.0, 10.0, 100.0, 1000.0, 10000.0];

        // Falls back to the 1x slot when the engine's current multiplier doesn't
        // land on a preset (e.g. right after loading) or while Max is active and
        // holding the pre-Max value.
        private static int SpeedIndex(SimulationSnapshot s)
        {
            var i = Array.FindIndex(Speeds, sp => Math.Abs(sp - s.SpeedMultiplier) < 0.001);
            return i >= 0 ? i : Array.IndexOf(Speeds, 1.0);
        }

        private void DecreaseSpeed(SimulationSnapshot s) => Service.SetSpeed(Speeds[Math.Max(0, SpeedIndex(s) - 1)]);

        private void IncreaseSpeed(SimulationSnapshot s) => Service.SetSpeed(Speeds[Math.Min(Speeds.Length - 1, SpeedIndex(s) + 1)]);

        private static string FormatSpeed(double v) => (v < 1 ? v.ToString("0.#", CultureInfo.InvariantCulture) : v.ToString("0", CultureInfo.InvariantCulture)) + "x";

        // Max: uncapped backend throughput (replaces the old separate Turbo
        // button -- hiding the chart for faster high-speed rendering is now just
        // the existing Hide Chart toggle, used independently of speed).
        private void SetMax() => Service.SetMaxSpeed();

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

        // Derived once per render from the selected trader's full closed-trade log
        // (TraderSnapshot.Trades) plus the equity extremes Trader tracks incrementally.
        // See TradeStatistics for the metrics themselves and their category grouping.
        private TradeStatistics? Stats =>
            SelectedTrader is not { } trader
                ? null
                : TradeStatistics.From(trader.Trades, trader.MaxEquity, trader.MaxDrawdown);

        private IReadOnlyList<StatGroup> StatGroups => Stats?.Groups ?? [];

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
