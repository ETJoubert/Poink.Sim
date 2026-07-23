using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Training;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Pages
{
    public partial class SimulationPage : ComponentBase, IDisposable, IAsyncDisposable
    {
        [Inject] private SimulationEngine Service { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

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
        // Two selectors share this role depending on Config.Aggregate: bar-mode runs use
        // a bar count (below), raw-tick runs fall back to this literal tick count since
        // there are no bars to derive one from (SimulationEngine.RecentBars stays empty
        // when Aggregate is off).
        private const int MaxTickWindow = 5000; // matches SimulationEngine.MaxRecentTicks
        private int _tickWindowSize = 200;
        private int ClampedTickWindow => Math.Clamp(_tickWindowSize, 2, MaxTickWindow);
        private double TickWindowFillPercent => ClampedTickWindow * 100.0 / MaxTickWindow;

        // Bar-mode counterpart to the tick-count fallback above -- counts bars back
        // from now rather than ticks. The volume profile itself is fed by merging
        // these bars' own price histograms, see MergedBarSamples below.
        private const int MaxBarWindow = 300; // matches SimulationEngine.MaxRecentBars
        private int _barWindowSize = 50;
        private int ClampedBarWindow => Math.Clamp(_barWindowSize, 1, MaxBarWindow);
        private double BarWindowFillPercent => ClampedBarWindow * 100.0 / MaxBarWindow;

        // Merges the last ClampedBarWindow bars' price histograms into one weighted-sample
        // list for TickVolumeProfileChart -- see BarPriceHistogram.Merge for how bars
        // sharing one BucketWidth get summed bucket-for-bucket instead of needing the
        // original per-tick prices back.
        private List<WeightedPriceSample> MergedBarSamples(SimulationSnapshot s) =>
            BarPriceHistogram.Merge(s.RecentBarHistograms, ClampedBarWindow)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();

        // Timestamp of the bar that starts the current window -- null once the window
        // covers the whole buffer, so OhlcvChart knows to hide the marker rather than
        // drawing it at the very first bar. Bars' own OpenTime is already a valid point
        // on the chart's time axis (unlike an arbitrary tick timestamp), so no snapping
        // is needed here the way the old tick-based version required.
        private DateTime? WindowStartUtc(IReadOnlyList<HlocAvdtBar> bars)
        {
            if (bars.Count <= ClampedBarWindow)
                return null;
            return bars[bars.Count - ClampedBarWindow].OpenTime;
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

            if (TrainingService.BrainOptions.Count > 0)
                _brainName = TrainingService.BrainOptions[0].Name;
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

        // ── Chart/volume-profile divider (drag to resize .sim-chart-side), and
        // chart-row/log-panel divider (drag to resize .sim-chart's height) ──

        // Purely a client-side layout preference -- nothing here round-trips back
        // into .NET state, so no [JSInvokable]/DotNetObjectReference is needed,
        // just eval + dispose.
        private ElementReference _chartDividerRef;
        private ElementReference _chartHDividerRef;
        private IJSObjectReference? _dividerResizeInstance;

        private const string ChartDividerScript = """
            (function() {
                function bind(divider) {
                    if (!divider || divider.dataset.bound) return;
                    divider.dataset.bound = '1';
                    var minWidth = parseInt(divider.dataset.minWidth, 10) || 160;
                    var maxWidth = parseInt(divider.dataset.maxWidth, 10) || 480;
                    divider.addEventListener('pointerdown', function(e) {
                        e.preventDefault();
                        var side = divider.nextElementSibling;
                        if (!side) return;
                        var startX = e.clientX;
                        var startWidth = side.getBoundingClientRect().width;
                        divider.setPointerCapture(e.pointerId);
                        divider.classList.add('sim-chart-divider-active');

                        function clamp(ev) {
                            return Math.min(maxWidth, Math.max(minWidth, startWidth - (ev.clientX - startX)));
                        }
                        function onMove(ev) {
                            side.style.width = clamp(ev) + 'px';
                        }
                        function onUp(ev) {
                            divider.releasePointerCapture(e.pointerId);
                            divider.removeEventListener('pointermove', onMove);
                            divider.removeEventListener('pointerup', onUp);
                            divider.classList.remove('sim-chart-divider-active');
                        }
                        divider.addEventListener('pointermove', onMove);
                        divider.addEventListener('pointerup', onUp);
                    });
                }
                function bindHeight(divider) {
                    if (!divider || divider.dataset.bound) return;
                    divider.dataset.bound = '1';
                    var minHeight = parseInt(divider.dataset.minHeight, 10) || 160;
                    var maxHeightVh = parseInt(divider.dataset.maxHeightVh, 10) || 80;
                    divider.addEventListener('pointerdown', function(e) {
                        e.preventDefault();
                        var target = divider.previousElementSibling;
                        if (!target) return;
                        var startY = e.clientY;
                        var startHeight = target.getBoundingClientRect().height;
                        var maxHeight = (maxHeightVh / 100) * window.innerHeight;
                        divider.setPointerCapture(e.pointerId);
                        divider.classList.add('sim-chart-hdivider-active');

                        function clamp(ev) {
                            return Math.min(maxHeight, Math.max(minHeight, startHeight + (ev.clientY - startY)));
                        }
                        function onMove(ev) {
                            target.style.height = clamp(ev) + 'px';
                        }
                        function onUp(ev) {
                            divider.releasePointerCapture(e.pointerId);
                            divider.removeEventListener('pointermove', onMove);
                            divider.removeEventListener('pointerup', onUp);
                            divider.classList.remove('sim-chart-hdivider-active');
                        }
                        divider.addEventListener('pointermove', onMove);
                        divider.addEventListener('pointerup', onUp);
                    });
                }
                return { bind: bind, bindHeight: bindHeight };
            })()
            """;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
                _dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartDividerScript);

            if (_snapshot is not null && !_hideChart && _dividerResizeInstance is not null)
            {
                await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef);
                await _dividerResizeInstance.InvokeVoidAsync("bindHeight", _chartHDividerRef);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_dividerResizeInstance is not null)
            {
                try { await _dividerResizeInstance.DisposeAsync(); }
                catch (JSDisconnectedException) { }
            }
        }

        // ── Saved brains (setup-only picker, see SavedBrainsTable) ──

        // Loading also selects the snapshot's architecture as this page's brain,
        // matching what the old plain <select> let you do directly -- picking a
        // saved brain here is both "load these weights" and "use this brain".
        private void UseSavedBrain(SavedBrainSummary summary)
        {
            if (TrainingService.LoadSavedBrain(summary.Id))
                _brainName = summary.BrainName;
        }

        private void DeleteSavedBrainSnapshot(SavedBrainSummary summary) => TrainingService.DeleteSavedBrain(summary.Id);

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

        // Plain nullable double (not a pre-formatted string) for RealizedPnl so the copied
        // DataTable's own N2 formatting applies -- DecisionLogCellClass below colors it by
        // sign, same pattern as TraderCardItem/TraderCellClass above.
        private sealed record DecisionLogRow(
            string Time, string Action, string Direction, string Size, string Price, double? RealizedPnl, string Reason);

        private static string FormatLogAction(string action) => action switch
        {
            "EnterLong" => "BUY",
            "EnterShort" => "SHORT",
            "Close" => "CLOSE",
            _ => action,
        };

        private List<DecisionLogRow> LogRows =>
            SelectedTrader is not { } trader
                ? new List<DecisionLogRow>()
                : trader.RecentLog
                    .Select(l => new DecisionLogRow(
                        l.TimestampUtc.ToString("HH:mm:ss"), FormatLogAction(l.Action), l.Direction.ToString(),
                        l.Size.ToString("F4"), l.Price.ToString("F5"), l.RealizedPnl, l.Reason ?? ""))
                    .Reverse()
                    .ToList();

        private static string DecisionLogCellClass(DecisionLogRow item, string propName) => propName switch
        {
            nameof(DecisionLogRow.RealizedPnl) => item.RealizedPnl is { } v ? DataTablePnlClass(v) : "",
            _ => "",
        };

        // v1 (Trader.cs) only ever holds one open Position at a time, so this is
        // 0-or-1 rows today; the table shape is what lets multi-position support
        // slot in later as "more rows" instead of a UI rework.
        //
        // UnrealizedPnl stays a plain nullable double (not a pre-formatted string) so the
        // copied DataTable's own N2 formatting applies -- PositionCellClass below colors it
        // by sign, same pattern as TraderCardItem/TraderCellClass above.
        private sealed record OpenPositionRow(string Direction, string Size, string EntryPrice, string EntryTime, double? UnrealizedPnl);

        private static string PositionCellClass(OpenPositionRow item, string propName) => propName switch
        {
            nameof(OpenPositionRow.UnrealizedPnl) => item.UnrealizedPnl is { } v ? DataTablePnlClass(v) : "",
            _ => "",
        };

        // Derived once per render from the selected trader's full closed-trade log
        // (TraderSnapshot.Trades) plus the equity extremes Trader tracks incrementally.
        // See TradeStatistics for the metrics themselves and their category grouping.
        private TradeStatistics? Stats =>
            SelectedTrader is not { } trader
                ? null
                : TradeStatistics.From(trader.Trades, trader.MaxEquity, trader.MaxDrawdown);

        private IReadOnlyList<StatGroup> StatGroups => Stats?.Groups ?? [];

        private List<OpenPositionRow> OpenPositionRows =>
            SelectedTrader?.Position is not { } pos
                ? new List<OpenPositionRow>()
                : new List<OpenPositionRow>
                {
                    new OpenPositionRow(
                        pos.Direction.ToString(), pos.Size.ToString("F4"), pos.EntryPrice.ToString("F5"),
                        pos.EntryTime.ToString("HH:mm:ss"),
                        _snapshot?.CurrentPrice is { } price ? pos.UnrealizedPnl(price) : null),
                };
    }
}
