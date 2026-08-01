using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Transformer;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Pages
{
    // Batch (non-playback) counterpart to SimulationPage, same idea as ProfilerPage:
    // pick a symbol/date range, then look at its bar chart, volume profile, and time
    // profile -- no brain/trader/statistics/playback, and (unlike ProfilerPage) no
    // click-to-draw annotations or saved-drawing persistence either. Data loads
    // lazily in bar-aligned chunks (see TransformerService): LOAD fetches only a
    // recent slice for a fast first paint, and panning the chart back toward its
    // oldest bar backfills the next older chunk on demand (wired through
    // DrawableOhlcvChart's OnNearStart -- see LoadMore below).
    public partial class TransformerPage : ComponentBase, IAsyncDisposable
    {
        [Inject] private ITransformerService Service { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        // How many bars each fetch (the initial LOAD or a pan-triggered backfill)
        // tries to gather -- generous enough to be immediately useful, small enough
        // that LOAD feels instant instead of aggregating the whole selected range
        // up front.
        private const int BarsPerFetch = 300;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private int _barMinutes = 1;

        private bool _isLoading;
        private bool _isLoadingMore;
        private string? _error;

        // false while Load() is populating the very first chunk (so DrawableOhlcvChart
        // fits/rezooms to it), true from then on so a LoadMore backfill prepends
        // older bars without disturbing the user's current pan/zoom -- see
        // DrawableOhlcvChart.KeepView.
        private bool _keepView;

        private TransformerConfig? _config;
        private List<HlocAvdtBar> _bars = new();
        private List<BarPriceHistogram> _barHistograms = new();
        private int _tickCount;
        private bool _reachedStart;

        // The Volume Range tool's current selection (see DrawableOhlcvChart's
        // ShowAnnotationTools="false" -- Volume Range is the only draw tool this page
        // exposes) -- both null means "no selection", which MergedSamples()/
        // MergedTimeSamples() treat as "use every bar loaded so far". Reset on a fresh
        // Load (new symbol/range). See ProfilerPage's own copy of this same field for
        // the full reasoning.
        private DateTime? _volumeRangeStart;
        private DateTime? _volumeRangeEnd;

        // Independent on/off switches for the three computed chart overlays -- same as
        // ProfilerPage's own copy. Not reset on Load -- these are display preferences.
        private bool _showProfilePanelLevels = true;
        private bool _showProfileChartLines = true;
        private bool _showVwap = true;

        // Raw ticks backing the tick-delta chart (see TickDeltaTransform) -- matches
        // SimulationEngine.MaxRecentTicks/SimulationPage.MaxTickWindow's own cap on how
        // many raw ticks are ever held in memory/rendered at once. (Re)loaded by
        // RefreshDeltaTicksAsync, both on a fresh Load and whenever the Volume Range
        // selection changes -- LoadMore's older backfill is the only thing that doesn't
        // touch it, since backfilling older bars changes neither the active selection
        // nor (when there isn't one) config.To.
        private const int MaxDeltaTicks = 5000;

        // Unused in practice (BuildDeltaBars never reads bar histograms), but
        // SimulationBarAggregator's constructor always takes one -- matches
        // TransformerService's own DefaultPriceBucketWidth.
        private const double DeltaBucketWidth = 0.01;

        private List<Tick> _deltaTicks = new();
        private int _deltaTicksPerBar = 50;
        private int _zScoreWindowSize = 50;
        private int _globalZScoreTicksPerBar = 50;
        private int _driftTicksPerBar = 50;
        private DriftMode _driftMode = DriftMode.Percentage;

        private bool HasDeltaData => _deltaTicks.Count >= 2;

        // Which of the four derived-series tabs is showing -- same tabbed-panel pattern
        // as ProfilerPage's own DRAWINGS/SAVED tabs, just this page's own copy (and its
        // own tabs) since neither page shares code-behind.
        private DataTab _activeDataTab;

        // LocalZScore/GlobalZScore exist side by side deliberately, not as a
        // toggle/replacement of one another -- see LocalWindowZScoreTransform vs
        // GlobalWindowZScoreTransform's doc comments for how their "window" scope
        // differs (many small independently-normalized sub-windows vs. one mean/stddev
        // over the whole selection).
        private enum DataTab { TickDelta, LocalZScore, GlobalZScore, Drift }

        // Chart/volume-profile divider and volume-profile/time-profile divider (drag to
        // resize their .trf-chart-side panels), plus the chart-row/delta-chart divider
        // (drag to resize .trf-chart's height) -- same pattern as ProfilerPage's own
        // dividers, just this page's own copy since none of the three pages share
        // code-behind.
        private ElementReference _chartDividerRef;
        private ElementReference _chartDividerRef2;
        private ElementReference _chartHDividerRef;
        private IJSObjectReference? _dividerResizeInstance;

        private const string ChartDividerScript = """
            (function() {
                function bind(divider) {
                    if (!divider || divider.dataset.bound) return;
                    divider.dataset.bound = '1';
                    var minWidth = parseInt(divider.dataset.minWidth, 10) || 200;
                    var maxWidth = parseInt(divider.dataset.maxWidth, 10) || 320;
                    divider.addEventListener('pointerdown', function(e) {
                        e.preventDefault();
                        var side = divider.nextElementSibling;
                        if (!side) return;
                        var startX = e.clientX;
                        var startWidth = side.getBoundingClientRect().width;
                        divider.setPointerCapture(e.pointerId);
                        divider.classList.add('trf-chart-divider-active');

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
                            divider.classList.remove('trf-chart-divider-active');
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
                        divider.classList.add('trf-chart-hdivider-active');

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
                            divider.classList.remove('trf-chart-hdivider-active');
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

            if (HasData && _dividerResizeInstance is not null)
            {
                await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef);
                await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef2);
                if (HasDeltaData)
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

        private bool HasData => _config is not null && _bars.Count > 0;

        private string StateClass() =>
            _isLoading ? "trf-state-loading" : HasData ? "trf-state-ready" : "trf-state-setup";

        private async Task Load()
        {
            _error = null;

            if (string.IsNullOrWhiteSpace(_table))
            { _error = "pick a symbol"; return; }
            if (_to <= _from)
            { _error = "To must be after From"; return; }

            _isLoading = true;
            _keepView = false;
            _config = null;
            _bars = new();
            _barHistograms = new();
            _tickCount = 0;
            _reachedStart = false;
            _volumeRangeStart = null;
            _volumeRangeEnd = null;
            _deltaTicks = new();

            try
            {
                var config = new TransformerConfig(_table, _from, _to, TimeSpan.FromMinutes(_barMinutes));
                var chunk = await Service.LoadAsync(config, config.To, BarsPerFetch);
                if (chunk.Bars.Count == 0 && chunk.ReachedStart)
                {
                    _error = "no ticks in the selected range";
                }
                else
                {
                    _config = config;
                    ApplyChunk(chunk);
                    await RefreshDeltaTicksAsync();
                }
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Wired to DrawableOhlcvChart's OnNearStart (fires on pan/zoom close to the
        // oldest loaded bar) and to the manual LOAD OLDER button -- either way, fetches
        // the next chunk immediately before the currently-earliest loaded bar.
        private async Task LoadMore()
        {
            if (_config is null || _reachedStart || _isLoadingMore) return;
            _isLoadingMore = true;
            _keepView = true;

            try
            {
                var cursor = _bars.Count > 0 ? _bars[0].OpenTime : _config.To;
                var chunk = await Service.LoadAsync(_config, cursor, BarsPerFetch);
                ApplyChunk(chunk);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        // New list instances (not in-place mutation) so DrawableOhlcvChart's own
        // ReferenceEquals(_renderedBars, Bars) change-detection notices the update.
        private void ApplyChunk(TransformerChunk chunk)
        {
            _bars = chunk.Bars.Concat(_bars).ToList();
            _barHistograms = chunk.BarHistograms.Concat(_barHistograms).ToList();
            _tickCount += chunk.TickCount;
            _reachedStart = chunk.ReachedStart;
        }

        private List<CTraderBarRow> ToBarRows() =>
            _bars.Select(b => new CTraderBarRow(
                b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance)).ToList();

        // Wired to DrawableOhlcvChart's OnVolumeRangeDrawn -- its two clicks just replace
        // the active selection outright, which is what MergedSamples()/MergedTimeSamples()
        // filter _bars/_barHistograms by, and (via RefreshDeltaTicksAsync) which raw ticks
        // the tick-delta chart's "latest tick n" is drawn from. ClearVolumeRange is the
        // only other way these change.
        private async Task SetVolumeRange(ChartTimeRange range)
        {
            _volumeRangeStart = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
            _volumeRangeEnd = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
            await RefreshDeltaTicksAsync();
        }

        // Wired to DrawableOhlcvChart's CLEAR VOLUME RANGE button -- drops back to
        // MergedSamples()/MergedTimeSamples()'s (and RefreshDeltaTicksAsync's) default of
        // every loaded bar/the latest ticks overall.
        private async Task ClearVolumeRange()
        {
            _volumeRangeStart = null;
            _volumeRangeEnd = null;
            await RefreshDeltaTicksAsync();
        }

        // Refetches the tick-delta chart's raw ticks -- the last MaxDeltaTicks ticks
        // ending at the active Volume Range's end (or config.To if no selection is
        // active), then trimmed to that selection's start if one is active. Re-run
        // whenever the Volume Range selection changes (see SetVolumeRange/
        // ClearVolumeRange above) as well as on a fresh Load, so the tick-delta chart's
        // "latest tick n" always tracks whatever window the volume/time profile panels
        // are currently built from.
        private async Task RefreshDeltaTicksAsync()
        {
            if (_config is null) { _deltaTicks = new(); return; }

            var rangeEnd = _volumeRangeEnd ?? _config.To;

            try
            {
                var ticks = await Service.LoadRecentTicksAsync(_config, rangeEnd, MaxDeltaTicks);
                _deltaTicks = _volumeRangeStart is { } start
                    ? ticks.Where(t => t.Timestamp >= start).ToList()
                    : ticks.ToList();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        // Bar count and span of the active Volume Range selection, for both profile
        // panels' RangeLabel -- null while no selection is active, which hides that badge.
        private string? VolumeRangeLabel()
        {
            if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end) return null;
            var barCount = _bars.Count(b => b.OpenTime >= start && b.OpenTime <= end);
            return $"{barCount:N0} bars, {FormatSpan(end - start)}";
        }

        // The Volume Range tool's own highlighted band on the main chart.
        private List<ChartRangeFill> VolumeRangeFill()
        {
            if (_volumeRangeStart is { } start && _volumeRangeEnd is { } end)
                return new() { new ChartRangeFill(start, end, "VOLUME RANGE", "volume") };
            return new();
        }

        // Bars/histograms narrowed to the active Volume Range selection, or every loaded
        // bar if none is active -- shared by MergedSamples/MergedTimeSamples/ProfileLevelLines
        // below so all three read the exact same window.
        private (List<HlocAvdtBar> Bars, List<BarPriceHistogram> Histograms) VolumeRangeWindow()
        {
            if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end)
                return (_bars, _barHistograms);

            var filtered = _bars
                .Zip(_barHistograms, (bar, histogram) => (bar, histogram))
                .Where(t => t.bar.OpenTime >= start && t.bar.OpenTime <= end)
                .ToList();
            return (filtered.Select(t => t.bar).ToList(), filtered.Select(t => t.histogram).ToList());
        }

        // Merges the relevant loaded bars' price histograms into one weighted-sample list
        // for TickVolumeProfileChart -- narrowed to the Volume Range selection once one is
        // active (see VolumeRangeWindow), every loaded bar otherwise.
        private List<WeightedPriceSample> MergedSamples()
        {
            var (_, histograms) = VolumeRangeWindow();
            return BarPriceHistogram.Merge(histograms, histograms.Count)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();
        }

        // Time-profile counterpart to MergedSamples() above, for TickTimeProfileChart --
        // through BarPriceHistogram.MergeTime instead of Merge since dwell time needs
        // each bar's own OpenTime/CloseTime/Volume alongside its histogram, not just the
        // histogram's bucket counts.
        private List<WeightedTimeSample> MergedTimeSamples()
        {
            var (bars, histograms) = VolumeRangeWindow();
            return BarPriceHistogram.MergeTime(bars, histograms, histograms.Count)
                .Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
                .ToList();
        }

        // Point of Control/Value Area for whatever window MergedSamples() currently builds
        // the volume profile panel from -- projected onto the main chart so they read
        // directly against price action, not just inside the profile panel.
        private List<ChartHorizontalLine> ProfileLevelLines()
        {
            if (!_showProfileChartLines) return new();
            if (VolumeProfileStats.Compute(MergedSamples()) is not { } levels) return new();
            return new()
            {
                new ChartHorizontalLine(levels.Poc, "POC", "profile"),
                new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
                new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
            };
        }

        // Cumulative volume-weighted average price over the same window ProfileLevelLines
        // above uses -- HlocAvdtBar.Average/Volume already carry the tick-weighted mean
        // price and tick count each bar needs, so this needs no histogram data at all.
        private List<ChartLinePoint> VwapPoints()
        {
            if (!_showVwap) return new();

            var bars = _volumeRangeStart is { } start && _volumeRangeEnd is { } end
                ? _bars.Where(b => b.OpenTime >= start && b.OpenTime <= end)
                : _bars;

            var points = new List<ChartLinePoint>();
            double cumulativePriceVolume = 0, cumulativeVolume = 0;
            foreach (var bar in bars)
            {
                cumulativePriceVolume += bar.Average * bar.Volume;
                cumulativeVolume += bar.Volume;
                if (cumulativeVolume > 0)
                    points.Add(new ChartLinePoint(bar.OpenTime, cumulativePriceVolume / cumulativeVolume));
            }
            return points;
        }

        // "2h 15m" / "3d 4h" / "45m" / "12s" -- coarsest two units that keep a range's
        // span readable at a glance, for VolumeRangeLabel above.
        private static string FormatSpan(TimeSpan span) => span switch
        {
            { TotalDays: >= 1 } => $"{(int)span.TotalDays}d {span.Hours}h",
            { TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
            { TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m",
            _ => $"{(int)span.TotalSeconds}s",
        };

        // Runs a synthetic tick series (TickDeltaTransform/LocalWindowZScoreTransform's
        // output -- already in tick-count order, not wall-clock time) through the same
        // SimulationBarAggregator every other bar chart on this page uses, bucketed by
        // the given tick-count bar size rather than a wall-clock one.
        private static List<HlocAvdtBar> AggregateSyntheticSeries(List<Tick> series, int barSizeInTicks)
        {
            if (series.Count == 0) return new();

            var barSize = TimeSpan.FromSeconds(Math.Max(1, barSizeInTicks));
            var aggregator = new SimulationBarAggregator(barSize, DeltaBucketWidth);
            var bars = new List<HlocAvdtBar>();
            foreach (var tick in series)
            {
                if (aggregator.Feed(tick) is { } completed)
                    bars.Add(completed.Bar);
            }
            if (aggregator.CurrentPartialBar is { } partial) bars.Add(partial);
            return bars;
        }

        private static List<CTraderBarRow> ToBarRows(List<HlocAvdtBar> bars) =>
            bars.Select(b => new CTraderBarRow(
                b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance)).ToList();

        private List<CTraderBarRow> DeltaBarRows() =>
            ToBarRows(AggregateSyntheticSeries(TickDeltaTransform.BuildDeltaSeries(_deltaTicks), _deltaTicksPerBar));

        // Local Z-Score's window size doubles as the aggregator's own bar size (see
        // LocalWindowZScoreTransform's doc comment on why those must match) -- each
        // resulting bar is exactly one sub-window's own normalization.
        private List<CTraderBarRow> ZScoreBarRows() =>
            ToBarRows(AggregateSyntheticSeries(
                LocalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks, Math.Max(1, _zScoreWindowSize)), _zScoreWindowSize));

        // Same per-window-normalized series the Local Z-Score chart's bars are built
        // from (see ZScoreBarRows above), fed to TickVolumeProfileChart the same raw
        // per-tick way GlobalZScoreProfileValues feeds the Global Z-Score profile --
        // see that method's own comment for why no bar-histogram merging is needed here.
        private List<double> LocalZScoreProfileValues() =>
            LocalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks, Math.Max(1, _zScoreWindowSize)).Select(t => t.Bid).ToList();

        private double? LocalZScoreCurrentValue()
        {
            var series = LocalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks, Math.Max(1, _zScoreWindowSize));
            return series.Count > 0 ? series[^1].Bid : null;
        }

        // Global Z-Score has only one window (the whole of _deltaTicks -- see
        // GlobalWindowZScoreTransform), so _globalZScoreTicksPerBar is purely a charting
        // bucket size, unrelated to (and not required to match) anything about how the
        // normalization itself was computed.
        private List<CTraderBarRow> GlobalZScoreBarRows() =>
            ToBarRows(AggregateSyntheticSeries(
                GlobalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks), _globalZScoreTicksPerBar));

        // Same z-score series the Global Z-Score chart's bars are built from (see
        // GlobalZScoreBarRows above), fed to TickVolumeProfileChart as raw per-tick
        // values instead of merged bar histograms -- GlobalWindowZScoreTransform has no
        // bar-level histogram of its own, and the series is already capped at
        // MaxDeltaTicks, well under TickVolumeProfileChart's own MaxWindow, so nothing
        // here needs bucket-summing the way MergedSamples does.
        private List<double> GlobalZScoreProfileValues() =>
            GlobalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks).Select(t => t.Bid).ToList();

        // The latest tick's own z-score, so the profile panel can highlight "where the
        // current normalized value sits" the same way the other profile panels highlight
        // CurrentPrice.
        private double? GlobalZScoreCurrentValue()
        {
            var series = GlobalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks);
            return series.Count > 0 ? series[^1].Bid : null;
        }

        private List<CTraderBarRow> DriftBarRows() =>
            ToBarRows(AggregateSyntheticSeries(
                DriftFromCurrentPriceTransform.BuildDriftSeries(_deltaTicks, _driftMode), _driftTicksPerBar));

        // Same drift series the Drift chart's bars are built from (see DriftBarRows
        // above), fed to TickVolumeProfileChart the same raw per-tick way
        // GlobalZScoreProfileValues feeds the Global Z-Score profile -- see that
        // method's own comment for why no bar-histogram merging is needed here.
        private List<double> DriftProfileValues() =>
            DriftFromCurrentPriceTransform.BuildDriftSeries(_deltaTicks, _driftMode).Select(t => t.Bid).ToList();

        private double? DriftCurrentValue()
        {
            var series = DriftFromCurrentPriceTransform.BuildDriftSeries(_deltaTicks, _driftMode);
            return series.Count > 0 ? series[^1].Bid : null;
        }

        // Reference line at 0 -- shared by the Tick Delta chart (above it means price back
        // then was higher than the latest tick/price has fallen to reach now, below it
        // means price back then was lower/price has risen) and the Drift chart (above it
        // means price back then was higher than the anchor, below it means lower -- 0
        // itself is the anchor price, whether expressed as a fraction or a log ratio).
        // Its own single-element list rather than a static constant so DrawableOhlcvChart
        // still sees a fresh instance every render, same as every other HorizontalLines
        // producer on this page.
        private static List<ChartHorizontalLine> ZeroLine() => new() { new ChartHorizontalLine(0, "ZERO") };

        private double? LastClose => _bars.Count > 0 ? _bars[^1].Close : null;
    }
}
