using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Profiler;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Pages
{
    // Batch (non-playback) counterpart to SimulationPage: pick a symbol/date range,
    // then look at its bar chart and volume profile -- no brain/trader/statistics/
    // playback. Data loads lazily in bar-aligned chunks (see ProfilerService):
    // LOAD fetches only a recent slice for a fast first paint, and panning the
    // chart back toward its oldest bar backfills the next older chunk on demand
    // (wired through DrawableOhlcvChart's OnNearStart -- see LoadMore below).
    public partial class ProfilerPage : ComponentBase, IAsyncDisposable
    {
        [Inject] private ProfilerService Service { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        // How many bars each fetch (the initial LOAD or a pan-triggered backfill)
        // tries to gather -- generous enough to be immediately useful, small enough
        // that LOAD feels instant instead of aggregating the whole selected range
        // up front.
        private const int BarsPerFetch = 300;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;
        private string _barMinutesText = "1";

        private bool _isLoading;
        private bool _isLoadingMore;
        private string? _error;

        // false while Load() is populating the very first chunk (so DrawableOhlcvChart
        // fits/rezooms to it), true from then on so a LoadMore backfill prepends
        // older bars without disturbing the user's current pan/zoom -- see
        // DrawableOhlcvChart.KeepView.
        private bool _keepView;

        private ProfilerConfig? _config;
        private List<HlocAvdtBar> _bars = new();
        private List<BarPriceHistogram> _barHistograms = new();
        private int _tickCount;
        private bool _reachedStart;

        // One user-placed annotation -- H-Line/V-Line/D-Line each end up with exactly one
        // line in exactly one of the three lists; T-Range/P-Range/Rectangle place 2 or
        // 2+2 of them per drawing (see DrawableOhlcvChart's DrawMode), grouped here into
        // one Drawing so the side panel shows (and removes) them as a single card instead
        // of exposing the individual lines a multi-click tool happened to be built from.
        // ChartBars is T-Range Chart's own addition -- the same two vertical lines T-Range
        // places, plus a snapshot of the loaded bars between them for a mini candlestick
        // chart in its card (see AddTRangeChart). Id/RangeBarCount/RangeSpan are only set
        // once a drawing is actually sealed (see FinishDrawing) -- 0/null while still
        // _pending, since a range's bar count/span isn't final until its second click lands.
        private sealed record Drawing(
            List<ChartHorizontalLine> HorizontalLines, List<ChartVerticalLine> VerticalLines,
            List<ChartDiagonalLine> DiagonalLines, List<CTraderBarRow> ChartBars)
        {
            public int Id { get; init; }
            public int? RangeBarCount { get; init; }
            public TimeSpan? RangeSpan { get; init; }

            public string Kind => this switch
            {
                { ChartBars.Count: > 0 } => "T-RANGE CHART",
                { HorizontalLines.Count: 1, VerticalLines.Count: 0, DiagonalLines.Count: 0 } => "H-LINE",
                { HorizontalLines.Count: 0, VerticalLines.Count: 1, DiagonalLines.Count: 0 } => "V-LINE",
                { HorizontalLines.Count: 0, VerticalLines.Count: 0, DiagonalLines.Count: 1 } => "D-LINE",
                { HorizontalLines.Count: 2, VerticalLines.Count: 0 } => "P-RANGE",
                { HorizontalLines.Count: 0, VerticalLines.Count: 2 } => "T-RANGE",
                { HorizontalLines.Count: 2, VerticalLines.Count: 2 } => "RECTANGLE",
                _ => "DRAWING",
            };

            // Drives both the drawn shape's colour on the chart (see AllHorizontalLines/
            // AllVerticalLines/AllDiagonalLines/AllRangeFills below) and matches
            // DrawableOhlcvChart.razor.css's .doc-tool-btn-* toolbar groups, so a drawing
            // always reads in the same colour as the button that placed it.
            public string? ColorGroup => Kind switch
            {
                "H-LINE" or "V-LINE" or "D-LINE" => "lines",
                "T-RANGE" or "P-RANGE" => "ranges",
                "RECTANGLE" => "shapes",
                "T-RANGE CHART" => "charts",
                _ => null,
            };

            public bool IsRange => VerticalLines.Count == 2 && Kind is "T-RANGE" or "T-RANGE CHART";
        }

        // Completed drawings (reset on a fresh Load, kept across LoadMore backfills since
        // they mark specific price/time points on data that's still on screen) plus the
        // one currently being placed -- DrawableOhlcvChart fires each line the instant
        // it's clicked (see Add*LineDrawn below), so _pending accumulates a multi-click
        // tool's lines as they land and only moves into _drawings once DrawableOhlcvChart's
        // OnDrawFinished says the whole shape is done (see FinishDrawing).
        private List<Drawing> _drawings = new();
        private Drawing _pending = NewPending();
        private int _nextDrawingId = 1;

        private static Drawing NewPending() => new(new(), new(), new(), new());

        private IEnumerable<Drawing> AllDrawings() => _drawings.Append(_pending);

        // ── Chart-row/drawings-panel divider (drag to resize .prof-chart's height) --
        // same pattern as SimulationPage's chart-row/log-panel divider (see its
        // ChartDividerScript's bindHeight), just this page's own copy since neither page
        // shares code-behind. Purely a client-side layout preference -- nothing here
        // round-trips back into .NET state, so no [JSInvokable]/DotNetObjectReference is
        // needed, just eval + dispose. ──────────────────────────────────────────────────
        private ElementReference _chartHDividerRef;
        private IJSObjectReference? _dividerResizeInstance;

        private const string ChartHDividerScript = """
            (function() {
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
                        divider.classList.add('prof-chart-hdivider-active');

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
                            divider.classList.remove('prof-chart-hdivider-active');
                        }
                        divider.addEventListener('pointermove', onMove);
                        divider.addEventListener('pointerup', onUp);
                    });
                }
                return { bindHeight: bindHeight };
            })()
            """;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
                _dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartHDividerScript);

            if (HasData && _dividerResizeInstance is not null)
                await _dividerResizeInstance.InvokeVoidAsync("bindHeight", _chartHDividerRef);
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
            _isLoading ? "prof-state-loading" : HasData ? "prof-state-ready" : "prof-state-setup";

        private async Task Load()
        {
            _error = null;

            if (string.IsNullOrWhiteSpace(_table))
            { _error = "pick a symbol"; return; }
            if (_to <= _from)
            { _error = "To must be after From"; return; }
            if (!int.TryParse(_barMinutesText, out var barMinutes) || barMinutes < 1)
                barMinutes = 1;

            _isLoading = true;
            _keepView = false;
            _config = null;
            _bars = new();
            _barHistograms = new();
            _tickCount = 0;
            _reachedStart = false;
            _drawings = new();
            _pending = NewPending();
            _nextDrawingId = 1;

            try
            {
                var config = new ProfilerConfig(_table, _from, _to, TimeSpan.FromMinutes(barMinutes));
                var chunk = await Service.LoadAsync(config, config.To, BarsPerFetch);
                if (chunk.Bars.Count == 0 && chunk.ReachedStart)
                {
                    _error = "no ticks in the selected range";
                }
                else
                {
                    _config = config;
                    ApplyChunk(chunk);
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
        private void ApplyChunk(ProfilerChunk chunk)
        {
            _bars = chunk.Bars.Concat(_bars).ToList();
            _barHistograms = chunk.BarHistograms.Concat(_barHistograms).ToList();
            _tickCount += chunk.TickCount;
            _reachedStart = chunk.ReachedStart;
        }

        // Wired to DrawableOhlcvChart's OnHorizontalLineDrawn/OnVerticalLineDrawn/
        // OnDiagonalLineDrawn -- fired immediately for each line as the user places it
        // (see drawableOhlcvChart.js's "draw on each click"), so these just grow the
        // in-progress _pending drawing; FinishDrawing below is what seals it into
        // _drawings once the whole click sequence for the armed tool is done.
        private void AddHorizontalLine(double price) => _pending.HorizontalLines.Add(new ChartHorizontalLine(price, price.ToString("F5")));

        private void AddVerticalLine(DateTime timeUtc) =>
            _pending.VerticalLines.Add(new ChartVerticalLine(timeUtc, timeUtc.ToString("yyyy-MM-dd HH:mm")));

        private void AddDiagonalLine(ChartDiagonalLine line)
        {
            var label = $"{line.StartPrice:F5} @ {line.StartTimeUtc:HH:mm} -> {line.EndPrice:F5} @ {line.EndTimeUtc:HH:mm}";
            _pending.DiagonalLines.Add(line with { Label = label });
        }

        // Wired to DrawableOhlcvChart's OnTRangeChartDrawn -- fired alongside the same two
        // OnVerticalLineDrawn calls T-Range itself would make for these two clicks (see
        // drawableOhlcvChart.js's 'trangechart' case), so _pending already has its two
        // ChartVerticalLine entries by the time this runs; this just adds the mini chart's
        // own bar snapshot on top, from whatever's loaded between the (possibly reversed)
        // click order.
        private void AddTRangeChart(ChartTimeRange range)
        {
            var start = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
            var end = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
            var slice = _bars.Where(b => b.OpenTime >= start && b.OpenTime <= end);
            _pending.ChartBars.AddRange(ToBarRows(slice));
        }

        // Wired to DrawableOhlcvChart's OnDrawFinished -- fires once per completed
        // placement (a lone H-Line/V-Line/D-Line, or a full T-Range/P-Range/Rectangle
        // sequence), sealing whatever _pending accumulated into one Drawing/card: gives
        // it its permanent #Id, and -- for T-Range/T-Range Chart specifically -- how many
        // loaded bars fall inside its two vertical lines and the span between them.
        private void FinishDrawing()
        {
            if (_pending.HorizontalLines.Count > 0 || _pending.VerticalLines.Count > 0 || _pending.DiagonalLines.Count > 0)
            {
                var drawing = _pending with { Id = _nextDrawingId++ };

                if (drawing.VerticalLines.Count == 2)
                {
                    var t1 = drawing.VerticalLines[0].TimeUtc;
                    var t2 = drawing.VerticalLines[1].TimeUtc;
                    var start = t1 <= t2 ? t1 : t2;
                    var end = t1 <= t2 ? t2 : t1;
                    drawing = drawing with
                    {
                        RangeBarCount = _bars.Count(b => b.OpenTime >= start && b.OpenTime <= end),
                        RangeSpan = end - start,
                    };
                }

                _drawings = _drawings.Append(drawing).ToList();
            }
            _pending = NewPending();
        }

        // Removes a whole Drawing (all of its lines together) -- the side panel gives
        // each card exactly one remove button rather than one per underlying line.
        private void RemoveDrawing(int index) => _drawings = _drawings.Where((_, i) => i != index).ToList();

        // Flattened for DrawableOhlcvChart, which only ever renders plain lines/shapes/
        // fills and has no notion of a "drawing" grouping -- includes _pending too, so a
        // multi-click tool's lines still appear on the chart as each one is placed, before
        // the group is sealed. Each line is tagged with its owning drawing's ColorGroup so
        // the chart colours it the same as the toolbar button that placed it (see "Show
        // colours on chart respective of type"). New list instances every call (not
        // cached), same reference-equality reasoning ApplyChunk/Add*Line above rely on.
        private List<ChartHorizontalLine> AllHorizontalLines() =>
            AllDrawings().SelectMany(d => d.HorizontalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

        private List<ChartVerticalLine> AllVerticalLines() =>
            AllDrawings().SelectMany(d => d.VerticalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

        private List<ChartDiagonalLine> AllDiagonalLines() =>
            AllDrawings().SelectMany(d => d.DiagonalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

        // One soft translucent band per T-Range/T-Range Chart drawing (see Drawing.IsRange)
        // -- "#Id" labels it on the chart itself, blank while still _pending (Id isn't
        // assigned until FinishDrawing seals it).
        private List<ChartRangeFill> AllRangeFills() =>
            AllDrawings()
                .Where(d => d.IsRange)
                .Select(d => new ChartRangeFill(
                    d.VerticalLines[0].TimeUtc, d.VerticalLines[1].TimeUtc, d.Id > 0 ? $"#{d.Id}" : null, d.ColorGroup))
                .ToList();

        // "2h 15m" / "3d 4h" / "45m" / "12s" -- coarsest two units that keep a range's
        // span readable at a glance, for the card summary (see "bar count and Time").
        private static string FormatSpan(TimeSpan span) => span switch
        {
            { TotalDays: >= 1 } => $"{(int)span.TotalDays}d {span.Hours}h",
            { TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
            { TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m",
            _ => $"{(int)span.TotalSeconds}s",
        };

        private List<CTraderBarRow> ToBarRows() => ToBarRows(_bars);

        private static List<CTraderBarRow> ToBarRows(IEnumerable<HlocAvdtBar> bars) =>
            bars.Select(b => new CTraderBarRow(
                b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance)).ToList();

        // Merges every loaded bar's price histogram into one weighted-sample list for
        // TickVolumeProfileChart -- see BarPriceHistogram.Merge for how bars sharing one
        // BucketWidth get summed bucket-for-bucket instead of needing the original
        // per-tick prices back. Always covers everything loaded so far (grows as more
        // history backfills in), same as ToBarRows above.
        private List<WeightedPriceSample> MergedSamples() =>
            BarPriceHistogram.Merge(_barHistograms, _barHistograms.Count)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();

        private double? LastClose => _bars.Count > 0 ? _bars[^1].Close : null;
    }
}
