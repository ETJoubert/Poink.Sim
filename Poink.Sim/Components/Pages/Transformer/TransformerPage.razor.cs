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

namespace Poink.Sim.Components.Pages
{

    public partial class TransformerPage : ComponentBase, IAsyncDisposable
    {
        [Inject] private ITransformerService Service { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;


        private const int BarsPerFetch = 300;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private int _barSizeSeconds = 60;

        private bool _isLoading;
        private bool _isLoadingMore;
        private string? _error;


        private bool _keepView;

        private TransformerConfig? _config;
        private List<HlocAvdtBar> _bars = new();
        private List<BarPriceHistogram> _barHistograms = new();
        private int _tickCount;
        private bool _reachedStart;


        private DateTime? _volumeRangeStart;
        private DateTime? _volumeRangeEnd;


        private bool _showProfilePanelLevels = true;
        private bool _showProfileChartLines = true;
        private bool _showVwap = true;


        private const int MaxDeltaTicks = 5000;


        private const double DeltaBucketWidth = 0.01;

        private List<Tick> _deltaTicks = new();
        private int _deltaTicksPerBar = 50;
        private int _zScoreWindowSize = 50;
        private int _globalZScoreTicksPerBar = 50;
        private int _driftTicksPerBar = 50;
        private DriftMode _driftMode = DriftMode.Percentage;


        private List<Tick> _subchartTicks = new();


        private int _subchartBarSizeSeconds = 60;

        private bool HasDeltaData => _deltaTicks.Count >= 2;


        private DataTab _activeDataTab;


        private enum DataTab { TickDelta, LocalZScore, GlobalZScore, Drift, Subchart }


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
                try
                { await _dividerResizeInstance.DisposeAsync(); }
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
            _subchartTicks = new();

            try
            {
                var config = new TransformerConfig(_table, _from, _to, TimeSpan.FromSeconds(_barSizeSeconds));
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
                    await RefreshSubchartTicksAsync();
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


        private async Task LoadMore()
        {
            if (_config is null || _reachedStart || _isLoadingMore)
                return;
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


        private async Task SetVolumeRange(ChartTimeRange range)
        {
            _volumeRangeStart = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
            _volumeRangeEnd = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
            await RefreshDeltaTicksAsync();
            await RefreshSubchartTicksAsync();
        }


        private async Task ClearVolumeRange()
        {
            _volumeRangeStart = null;
            _volumeRangeEnd = null;
            await RefreshDeltaTicksAsync();
            await RefreshSubchartTicksAsync();
        }


        private async Task RefreshDeltaTicksAsync()
        {
            if (_config is null)
            { _deltaTicks = new(); return; }

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


        private async Task RefreshSubchartTicksAsync()
        {
            if (_config is null || _volumeRangeStart is not { } start || _volumeRangeEnd is not { } end)
            {
                _subchartTicks = new();
                return;
            }

            try
            {
                var ticks = await Service.LoadTicksInRangeAsync(_config, start, end);
                _subchartTicks = ticks.ToList();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }


        private string? VolumeRangeLabel()
        {
            if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end)
                return null;
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


        private List<WeightedPriceSample> MergedSamples()
        {
            var (_, histograms) = VolumeRangeWindow();
            return BarPriceHistogram.Merge(histograms, histograms.Count)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();
        }


        private List<WeightedTimeSample> MergedTimeSamples()
        {
            var (bars, histograms) = VolumeRangeWindow();
            return BarPriceHistogram.MergeTime(bars, histograms, histograms.Count)
                .Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
                .ToList();
        }


        private List<ChartHorizontalLine> ProfileLevelLines()
        {
            if (!_showProfileChartLines)
                return new();
            if (VolumeProfileStats.Compute(MergedSamples()) is not { } levels)
                return new();
            return new()
            {
                new ChartHorizontalLine(levels.Poc, "POC", "profile"),
                new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
                new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
            };
        }


        private List<ChartLinePoint> VwapPoints()
        {
            if (!_showVwap)
                return new();

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


        private static string FormatSpan(TimeSpan span) => span switch
        {
            { TotalDays: >= 1 } => $"{(int)span.TotalDays}d {span.Hours}h",
            { TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
            { TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m",
            _ => $"{(int)span.TotalSeconds}s",
        };


        private static List<HlocAvdtBar> AggregateSyntheticSeries(List<Tick> series, int barSizeInTicks)
        {
            if (series.Count == 0)
                return new();

            var barSize = TimeSpan.FromSeconds(Math.Max(1, barSizeInTicks));
            var aggregator = new SimulationBarAggregator(barSize, DeltaBucketWidth);
            var bars = new List<HlocAvdtBar>();
            foreach (var tick in series)
            {
                if (aggregator.Feed(tick) is { } completed)
                    bars.Add(completed.Bar);
            }
            if (aggregator.CurrentPartialBar is { } partial)
                bars.Add(partial);
            return bars;
        }

        private static List<CTraderBarRow> ToBarRows(List<HlocAvdtBar> bars) =>
            bars.Select(b => new CTraderBarRow(
                b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance)).ToList();

        private List<CTraderBarRow> DeltaBarRows() =>
            ToBarRows(AggregateSyntheticSeries(TickDeltaTransform.BuildDeltaSeries(_deltaTicks), _deltaTicksPerBar));


        private List<CTraderBarRow> ZScoreBarRows() =>
            ToBarRows(AggregateSyntheticSeries(
                LocalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks, Math.Max(1, _zScoreWindowSize)), _zScoreWindowSize));


        private List<double> LocalZScoreProfileValues() =>
            LocalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks, Math.Max(1, _zScoreWindowSize)).Select(t => t.Bid).ToList();

        private double? LocalZScoreCurrentValue()
        {
            var series = LocalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks, Math.Max(1, _zScoreWindowSize));
            return series.Count > 0 ? series[^1].Bid : null;
        }


        private List<CTraderBarRow> GlobalZScoreBarRows() =>
            ToBarRows(AggregateSyntheticSeries(
                GlobalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks), _globalZScoreTicksPerBar));


        private List<double> GlobalZScoreProfileValues() =>
            GlobalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks).Select(t => t.Bid).ToList();


        private double? GlobalZScoreCurrentValue()
        {
            var series = GlobalWindowZScoreTransform.BuildZScoreSeries(_deltaTicks);
            return series.Count > 0 ? series[^1].Bid : null;
        }

        private List<CTraderBarRow> DriftBarRows() =>
            ToBarRows(AggregateSyntheticSeries(
                DriftFromCurrentPriceTransform.BuildDriftSeries(_deltaTicks, _driftMode), _driftTicksPerBar));


        private List<double> DriftProfileValues() =>
            DriftFromCurrentPriceTransform.BuildDriftSeries(_deltaTicks, _driftMode).Select(t => t.Bid).ToList();

        private double? DriftCurrentValue()
        {
            var series = DriftFromCurrentPriceTransform.BuildDriftSeries(_deltaTicks, _driftMode);
            return series.Count > 0 ? series[^1].Bid : null;
        }


        private List<CTraderBarRow> SubchartBarRows()
        {
            if (_subchartTicks.Count == 0)
                return new();

            var aggregator = new SimulationBarAggregator(TimeSpan.FromSeconds(Math.Max(1, _subchartBarSizeSeconds)), DeltaBucketWidth);
            var bars = new List<HlocAvdtBar>();
            foreach (var tick in _subchartTicks)
            {
                if (aggregator.Feed(tick) is { } completed)
                    bars.Add(completed.Bar);
            }
            if (aggregator.CurrentPartialBar is { } partial)
                bars.Add(partial);
            return ToBarRows(bars);
        }


        private static List<ChartHorizontalLine> ZeroLine() => new() { new ChartHorizontalLine(0, "ZERO") };

        private double? LastClose => _bars.Count > 0 ? _bars[^1].Close : null;
    }
}
