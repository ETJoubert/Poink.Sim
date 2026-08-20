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
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
    public partial class SimulationPage : ComponentBase, IDisposable, IAsyncDisposable
    {
        [Inject] private ISimulationEngine Service { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;


        [Inject] private IRlTrainingService TrainingService { get; set; } = default!;

        [Inject] private IBrainService BrainService { get; set; } = default!;


        [Inject] private IModelService ModelService { get; set; } = default!;

        [Inject] private IRiskPolicyService RiskPolicyService { get; set; } = default!;

        // ── Setup form state ──

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private bool _aggregate = true;
        private int _barSizeSeconds = 60;
        private SimulationSteppingMode _steppingMode = SimulationSteppingMode.Bar;
        private double _cellWidth = 0.10;
        private string _brainName = "";

        private long? _modelId;

        private double _startingCash = 10_000;
        private int _traderCount = 1;

        // ── Trader (CFD account) controls ──
        private double _leverage = 1;
        private double _commissionRatePercent = 0;
        private double _stopOutLevelPercent = 50;
        private double _pipSize = 0.01;

        private readonly List<string> _selectedRiskPolicies = new();
        private bool _lockRiskPolicies;

        private string? _loadError;

        // ── Live state (mirrors the service) ──

        private SimulationSnapshot? _snapshot;
        private bool _isLoading;
        private string? _error;
        private bool _hideChart;

        // Fits the chart once on a fresh run, then latches true so live ticks preserve the
        // user's own pan/zoom instead of refitting every tick. See MarketChart.KeepView.
        private bool _keepChartView;

        private bool _traderPanelCollapsed;
        private bool _setupCollapsed;
        private int? _selectedTraderId;
        private LogTab _activeLogTab;


        private const int MaxTickWindow = 5000; // matches SimulationEngine.MaxRecentTicks
        private int _tickWindowSize = 200;
        private int ClampedTickWindow => Math.Clamp(_tickWindowSize, 2, MaxTickWindow);
        private double TickWindowFillPercent => ClampedTickWindow * 100.0 / MaxTickWindow;


        private const int MaxBarWindow = 300; // matches SimulationEngine.MaxRecentBars
        private int _barWindowSize = 50;
        private int ClampedBarWindow => Math.Clamp(_barWindowSize, 1, MaxBarWindow);
        private double BarWindowFillPercent => ClampedBarWindow * 100.0 / MaxBarWindow;


        private bool _showProfilePanelLevels = true;
        private bool _showProfileChartLines = true;
        private bool _showVwap = true;

        private bool _showMainChart = true;
        private bool _showXGrid;
        private bool _showVolumeProfile;
        private bool _showTimeProfile;

        private int _xgridPipCount = 20;
        private int _xgridColCount = 20;

        // A lone visible panel fills the row instead of keeping its dragged/default width,
        // since a resize divider has nothing to divide against with only one panel showing.
        private int VisibleChartPanelCount(SimulationSnapshot s) =>
            (_showMainChart ? 1 : 0) +
            (s.Config.Aggregate && _showXGrid ? 1 : 0) +
            (_showVolumeProfile ? 1 : 0) +
            (_showTimeProfile ? 1 : 0);

        // A divider's negative margin assumes a panel sits to its left -- rendering one as the
        // row's first flex child pulls it past the container's own padding. Panels are ordered
        // main -> XGrid -> volume profile -> time profile, so each divider only shows once a
        // panel earlier in that order is actually visible for it to sit between.
        private bool ShowXGridDivider(SimulationSnapshot s) =>
            s.Config.Aggregate && _showXGrid && _showMainChart;

        private bool ShowVolumeProfileDivider(SimulationSnapshot s) =>
            _showVolumeProfile && (_showMainChart || (s.Config.Aggregate && _showXGrid));

        private bool ShowTimeProfileDivider(SimulationSnapshot s) =>
            _showTimeProfile && (_showMainChart || (s.Config.Aggregate && _showXGrid) || _showVolumeProfile);

        private XGridResult? _xgridCache;
        private (int Pips, double PipSize, int Cols, int Window, int Count, DateTime LastOpen, double LastClose, double LastVolume)? _xgridKey;

        // Row height comes from the account's own pip size (Config.PipSize, set up before
        // load, same value TraderControlsPanel uses for SL/TP), not a value XGrid owns itself.
        private XGridResult? ComputeXGrid(SimulationSnapshot s)
        {
            if (!s.Config.Aggregate || s.RecentBars.Count == 0)
                return null;
            var window = Math.Min(ClampedBarWindow, s.RecentBars.Count);
            var last = s.RecentBars[^1];
            var key = (_xgridPipCount, s.Config.PipSize, _xgridColCount, window, s.RecentBars.Count, last.OpenTime, last.Close, (double)last.Volume);
            if (_xgridKey == key && _xgridCache is not null)
                return _xgridCache;

            var start = s.RecentBars[s.RecentBars.Count - window].OpenTime;
            var end = last.OpenTime;
            _xgridKey = key;
            _xgridCache = XGridStats.Compute(
                s.RecentBars, s.RecentBarHistograms, MergedBarSamples(s), start, end, _xgridPipCount * s.Config.PipSize, _xgridColCount);
            return _xgridCache;
        }


        // Feeds the selected trader's open position and window-visible closed trades to XGrid
        // as price/time-anchored markers -- recomputed each render so live P&L colouring on the
        // open position stays current. Trades entirely outside the grid's window are dropped to
        // keep the overlay uncluttered.
        private List<XGridPosition> XGridPositions(SimulationSnapshot s)
        {
            var result = new List<XGridPosition>();
            if (SelectedTrader is not { } trader)
                return result;

            var window = Math.Min(ClampedBarWindow, s.RecentBars.Count);
            if (window == 0)
                return result;
            var start = s.RecentBars[s.RecentBars.Count - window].OpenTime;
            var end = s.RecentBars[^1].OpenTime;

            foreach (var trade in trader.Trades)
            {
                if (trade.ExitTime < start || trade.EntryTime > end)
                    continue;
                result.Add(new XGridPosition(
                    MapDirection(trade.Direction), trade.EntryTime, trade.EntryPrice, trade.ExitTime, trade.ExitPrice));
            }
            if (trader.Position is { } pos)
                result.Add(new XGridPosition(
                    MapDirection(pos.Direction), pos.EntryTime, pos.EntryPrice,
                    StopLoss: pos.StopLossPrice, TakeProfit: pos.TakeProfitPrice)
                { Id = "position" });
            return result;
        }

        private static XGridPositionDirection MapDirection(PositionDirection direction) =>
            direction == PositionDirection.Long ? XGridPositionDirection.Long : XGridPositionDirection.Short;

        private List<XGridPending> XGridPending()
        {
            var result = new List<XGridPending>();
            if (SelectedTrader?.PendingOrder is { } pending)
                result.Add(new XGridPending(
                    pending.Direction == PositionDirection.Long, pending.TriggerPrice, pending.CapitalFraction,
                    pending.StopLossPrice, pending.TakeProfitPrice)
                { Id = "pending" });
            return result;
        }

        private void CancelXGridPending(string id) => CloseSelectedTrader("xgrid-cancel");

        private void CloseXGridPosition(string id) => CloseSelectedTrader("xgrid-close");

        // Cancel & re-place: the engine has no in-place modify, so the resting order is closed
        // and a fresh one submitted with the edited values.
        private void ModifyXGridPending(XGridOrderModification modification)
        {
            CloseSelectedTrader("xgrid-modify");
            PlaceXGridOrder(new XGridOrder(
                modification.IsLong, modification.TriggerPrice, modification.Volume,
                modification.StopLoss, modification.TakeProfit));
        }

        private void CloseSelectedTrader(string reason)
        {
            if (SelectedTrader is not { } trader)
                return;
            Service.SubmitManualDecision(trader.Id, new SimulationDecision(SimulationAction.Close, Reason: reason), out _);
        }

        // Submits an XGrid right-margin order as a manual pending decision for the selected
        // trader. The entry price becomes a Limit trigger when it sits on the favourable side
        // of current price, a Stop otherwise; Volume maps onto the engine's capital fraction.
        private void PlaceXGridOrder(XGridOrder order)
        {
            if (SelectedTrader is not { } trader)
                return;

            double current = _snapshot?.CurrentPrice ?? order.EntryPrice;
            var action = order.IsLong ? SimulationAction.EnterLong : SimulationAction.EnterShort;
            OrderType orderType = order.IsLong
                ? (order.EntryPrice <= current ? OrderType.Limit : OrderType.Stop)
                : (order.EntryPrice >= current ? OrderType.Limit : OrderType.Stop);
            double fraction = Math.Clamp(order.Volume, 0.01, 1.0);

            var decision = new SimulationDecision(
                action, fraction, order.StopLoss, order.TakeProfit, orderType, order.EntryPrice, Reason: "xgrid");
            Service.SubmitManualDecision(trader.Id, decision, out _);
        }

        private List<WeightedPriceSample> MergedBarSamples(SimulationSnapshot s) =>
            BarPriceHistogram.Merge(s.RecentBarHistograms, ClampedBarWindow)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();


        private List<WeightedTimeSample> MergedBarTimeSamples(SimulationSnapshot s) =>
            BarPriceHistogram.MergeTime(s.RecentBars, s.RecentBarHistograms, ClampedBarWindow)
                .Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
                .ToList();

        private List<ChartHorizontalLine> ProfileLevelLines(SimulationSnapshot s)
        {
            if (!_showProfileChartLines)
                return new();
            if (VolumeProfileStats.Compute(MergedBarSamples(s)) is not { } levels)
                return new();
            return new()
            {
                new ChartHorizontalLine(levels.Poc, "POC", "profile"),
                new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
                new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
            };
        }


        private List<ChartLinePoint> VwapPoints(SimulationSnapshot s)
        {
            if (!_showVwap)
                return new();

            var bars = s.RecentBars;
            var windowBars = bars.Count > ClampedBarWindow ? bars.Skip(bars.Count - ClampedBarWindow) : bars;

            var points = new List<ChartLinePoint>();
            double cumulativePriceVolume = 0, cumulativeVolume = 0;
            foreach (var bar in windowBars)
            {
                cumulativePriceVolume += bar.Average * bar.Volume;
                cumulativeVolume += bar.Volume;
                if (cumulativeVolume > 0)
                    points.Add(new ChartLinePoint(bar.OpenTime, cumulativePriceVolume / cumulativeVolume));
            }
            return points;
        }

        private DateTime? WindowStartUtc(IReadOnlyList<HlocAvdtBar> bars)
        {
            if (bars.Count <= ClampedBarWindow)
                return null;
            return bars[bars.Count - ClampedBarWindow].OpenTime;
        }

        private enum LogTab { Decisions, Positions, Traders, Brain, InputsOutputs, Statistics }


        private sealed record SimulationBrainOption(string DisplayName, string BrainName, long? ModelId);

        private IReadOnlyList<SimulationBrainOption> SimulationBrainOptions =>
            ModelService.Models
                .Select(m => new SimulationBrainOption($"{m.Name} ({m.BrainName})", m.BrainName, m.Id))
                .Concat(BrainService.BrainOptions.Where(b => !b.IsNeuralNetwork).Select(b => new SimulationBrainOption(b.Name, b.Name, null)))
                .ToList();


        private static string OptionKey(SimulationBrainOption option) =>
            option.ModelId is { } id ? $"model:{id}" : $"heuristic:{option.BrainName}";

        private string SelectedOptionKey =>
            SimulationBrainOptions.FirstOrDefault(o => o.BrainName == _brainName && o.ModelId == _modelId) is { } selected
                ? OptionKey(selected)
                : "";

        private void OnSimulationBrainChanged(ChangeEventArgs e)
        {
            var option = SimulationBrainOptions.FirstOrDefault(o => OptionKey(o) == (e.Value?.ToString() ?? ""));
            if (option is null)
                return;

            _brainName = option.BrainName;
            _modelId = option.ModelId;
            if (option.ModelId is { } modelId)
                TrainingService.LoadSavedBrain(modelId);
        }


        private bool CurrentBrainIsNeuralNetwork(string brainName) =>
            BrainService.BrainOptions.FirstOrDefault(b => b.Name == brainName)?.IsNeuralNetwork ?? false;


        private TraderSnapshot? SelectedTrader =>
            _snapshot is null
                ? null
                : _snapshot.Traders.FirstOrDefault(t => t.Id == _selectedTraderId) ?? _snapshot.Traders.FirstOrDefault();

        private void SelectTrader(int id)
        {
            _selectedTraderId = id;
            Service.SelectTraderForIo(id);
        }


        private bool? SelectedPositionIsLong => SelectedTrader?.Position?.Direction switch
        {
            PositionDirection.Long => true,
            PositionDirection.Short => false,
            null => null,
        };


        private bool SelectedTraderIsManual => SelectedTrader?.Mode == TraderControlMode.Manual;

        private string? SelectedPositionSummary =>
            SelectedTrader?.Position is not { } pos ? null : $"{pos.Direction} {pos.Size:F4} @ {pos.EntryPrice:F5}";

        private string? SelectedPendingSummary =>
            SelectedTrader?.PendingOrder is not { } pending
                ? null
                : $"PENDING {pending.Direction} {pending.OrderType} @ {pending.TriggerPrice:F5}";

        // ── Lifecycle ──

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;
            ApplyServiceState();

            if (SimulationBrainOptions.Count > 0)
            {
                var first = SimulationBrainOptions[0];
                _brainName = first.BrainName;
                _modelId = first.ModelId;
                if (first.ModelId is { } modelId)
                    TrainingService.LoadSavedBrain(modelId);
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
        }


        private ElementReference _chartDividerRefXGrid;
        private ElementReference _chartDividerRef;
        private ElementReference _chartDividerRef2;
        private ElementReference _chartHDividerRef;
        private ElementReference _sidePanelDividerRef;
        private IJSObjectReference? _dividerResizeInstance;

        private const string ChartDividerScript = """
            (function() {
                function bind(divider) {
                    if (!divider || divider.dataset.bound) return;
                    divider.dataset.bound = '1';
                    var minWidth = parseInt(divider.dataset.minWidth, 10) || 160;
                    var maxWidth = parseInt(divider.dataset.maxWidth, 10) || Infinity;
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
                if (ShowXGridDivider(_snapshot))
                    await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRefXGrid);
                if (ShowVolumeProfileDivider(_snapshot))
                    await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef);
                if (ShowTimeProfileDivider(_snapshot))
                    await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef2);
                await _dividerResizeInstance.InvokeVoidAsync("bindHeight", _chartHDividerRef);
            }

            if (_snapshot is not null && !_traderPanelCollapsed && _dividerResizeInstance is not null)
                await _dividerResizeInstance.InvokeVoidAsync("bind", _sidePanelDividerRef);

            if (!_keepChartView && _snapshot is { Config.Aggregate: true } snap && !_hideChart && snap.RecentBars.Count > 0)
                _keepChartView = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_dividerResizeInstance is not null)
            {
                try
                { await _dividerResizeInstance.DisposeAsync(); }
                catch (JSDisconnectedException) { }
            }
        }

        // ── Saved brains (setup-only picker, see SavedBrainsTable) ──


        private void UseSavedBrain(TrainedModelSummary summary)
        {
            if (TrainingService.LoadSavedBrain(summary.Id))
            {
                _brainName = summary.BrainName;
                _modelId = summary.Id;
            }
        }

        private void DeleteSavedBrainSnapshot(TrainedModelSummary summary) => ModelService.DeleteModel(summary.Id);

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
            if (!ValidateTraderAccountParams())
                return;
            if (_traderCount < 1)
                _traderCount = 1;

            var config = new SimulationConfig(
                _table, _from, _to, _aggregate, TimeSpan.FromSeconds(_barSizeSeconds), _brainName, _startingCash, _traderCount,
                _leverage, _stopOutLevelPercent, _commissionRatePercent, _pipSize, SelectedRiskPolicies, _lockRiskPolicies,
                _steppingMode, _cellWidth);

            _keepChartView = false;
            _setupCollapsed = true;
            if (!Service.TryLoad(config))
                _loadError = "a load is already in progress";
        }


        private bool ValidateTraderAccountParams()
        {
            if (_leverage < 1)
            { _loadError = "leverage must be at least 1"; return false; }
            if (_commissionRatePercent < 0)
            { _loadError = "commission rate can't be negative"; return false; }
            if (_stopOutLevelPercent <= 0 || _stopOutLevelPercent > 100)
            { _loadError = "stop-out level must be between 0 and 100%"; return false; }
            if (_pipSize <= 0)
            { _loadError = "pip size must be positive"; return false; }
            return true;
        }


        private IReadOnlyList<string>? SelectedRiskPolicies =>
            _selectedRiskPolicies.Count > 0 ? _selectedRiskPolicies.ToList() : null;


        private void OnAggregationSettingChanged() => Service.SetAggregation(_aggregate, TimeSpan.FromSeconds(_barSizeSeconds));

        private void OnSteppingModeChanged() => Service.SetSteppingMode(_steppingMode, _cellWidth);

        // Pins the live cell's edges on XGrid as host lines, distinct from the grid's own
        // rowCount-derived bands, so a Cell-stepping run can be compared against them visually.
        private static List<XGridLine> CellBandLines(SimulationSnapshot s) =>
            s.Config.SteppingMode == SimulationSteppingMode.Cell && s.CellState is { } cell
                ? new List<XGridLine>
                {
                    XGridLine.Horizontal(cell.CellLow) with { Id = "cell-low" },
                    XGridLine.Horizontal(cell.CellHigh) with { Id = "cell-high" },
                }
                : new List<XGridLine>();


        private void LoadDummySimulation()
        {
            _loadError = null;

            if (string.IsNullOrWhiteSpace(_brainName))
            { _loadError = "pick a brain"; return; }
            if (_startingCash <= 0)
            { _loadError = "starting cash must be positive"; return; }
            if (!ValidateTraderAccountParams())
                return;
            if (_traderCount < 1)
                _traderCount = 1;

            var to = _to > _from ? _to : DateTime.UtcNow;
            var from = _to > _from ? _from : to.AddHours(-4);
            var table = string.IsNullOrWhiteSpace(_table) ? "DUMMY" : _table;

            var config = new SimulationConfig(
                table, from, to, _aggregate, TimeSpan.FromSeconds(_barSizeSeconds), _brainName, _startingCash, _traderCount,
                _leverage, _stopOutLevelPercent, _commissionRatePercent, _pipSize, SelectedRiskPolicies, _lockRiskPolicies,
                _steppingMode, _cellWidth);

            _keepChartView = false;
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


        private static readonly double[] Speeds = [0.1, 1.0, 10.0, 100.0, 1000.0, 10000.0];


        private static int SpeedIndex(SimulationSnapshot s)
        {
            var i = Array.FindIndex(Speeds, sp => Math.Abs(sp - s.SpeedMultiplier) < 0.001);
            return i >= 0 ? i : Array.IndexOf(Speeds, 1.0);
        }

        private void DecreaseSpeed(SimulationSnapshot s) => Service.SetSpeed(Speeds[Math.Max(0, SpeedIndex(s) - 1)]);

        private void IncreaseSpeed(SimulationSnapshot s) => Service.SetSpeed(Speeds[Math.Min(Speeds.Length - 1, SpeedIndex(s) + 1)]);

        private static string FormatSpeed(double v) => (v < 1 ? v.ToString("0.#", CultureInfo.InvariantCulture) : v.ToString("0", CultureInfo.InvariantCulture)) + "x";


        private void SetMax() => Service.SetMaxSpeed();

        private void ToggleHideChart() => _hideChart = !_hideChart;

        private void ToggleTraderPanel() => _traderPanelCollapsed = !_traderPanelCollapsed;

        private void ToggleSetupPanel() => _setupCollapsed = !_setupCollapsed;

        private void ResetSim()
        {
            _keepChartView = false;

            Service.Reset();
        }

        private void UnloadSim()
        {
            _keepChartView = false;
            _setupCollapsed = false;
            Service.Unload();
        }

        // ── Rendering helpers ──

        private static List<CTraderBarRow> ToBarRows(IReadOnlyList<HlocAvdtBar> bars) =>
            bars.Select(b => new CTraderBarRow(
                b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance)).ToList();

        private static List<double> ToMidPrices(IReadOnlyList<Tick> ticks) =>
            ticks.Select(t => (t.Bid + t.Ask) / 2.0).ToList();

        private static List<TimedPrice> ToTimedMidPrices(IReadOnlyList<Tick> ticks) =>
            ticks.Select(t => new TimedPrice(t.Timestamp, (t.Bid + t.Ask) / 2.0)).ToList();

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


        private sealed record TraderCardItem(
            int Id, string Trader, double Cash, double Realized, double Unrealized, double Equity,
            double MarginUsed, double? MarginLevel, string Position, int Trades);

        private List<TraderCardItem> TraderItems =>
            _snapshot is null
                ? new List<TraderCardItem>()
                : _snapshot.Traders
                    .Select(t => new TraderCardItem(
                        t.Id, t.Label, t.Capital.Cash, t.Capital.RealizedPnl, t.Capital.UnrealizedPnl,
                        t.Capital.Equity, t.Capital.MarginUsed, t.Capital.MarginLevel,
                        t.Position is null ? "flat" : t.Position.Value.Direction.ToString(), t.TradeCount))
                    .ToList();

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


        private sealed record DecisionLogRow(
            string Time, string Action, string Direction, string Size, string Price, double? RealizedPnl, double Commission, string Reason);

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
                        l.Size.ToString("F4"), l.Price.ToString("F5"), l.RealizedPnl, l.Commission, l.Reason ?? ""))
                    .Reverse()
                    .ToList();

        private static string DecisionLogCellClass(DecisionLogRow item, string propName) => propName switch
        {
            nameof(DecisionLogRow.RealizedPnl) => item.RealizedPnl is { } v ? DataTablePnlClass(v) : "",
            _ => "",
        };


        private sealed record OpenPositionRow(string Direction, string Size, string EntryPrice, string EntryTime, double? UnrealizedPnl);

        private static string PositionCellClass(OpenPositionRow item, string propName) => propName switch
        {
            nameof(OpenPositionRow.UnrealizedPnl) => item.UnrealizedPnl is { } v ? DataTablePnlClass(v) : "",
            _ => "",
        };


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
                        _snapshot?.CurrentBid is { } bid && _snapshot?.CurrentAsk is { } ask ? pos.UnrealizedPnl(bid, ask) : null),
                };
    }
}
