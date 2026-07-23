using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
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
        [Inject] private ITRangeChartRepository TRangeChartRepository { get; set; } = default!;
        [Inject] private ITRangeReferenceChartRepository TRangeReferenceChartRepository { get; set; } = default!;

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

        // Which of the three bottom panels is showing -- same tabbed-panel pattern as
        // SimulationPage's LogTab/_activeLogTab, just this page's own copy since neither
        // page shares code-behind.
        private PanelTab _activePanelTab;

        private enum PanelTab { Drawings, SavedTRangeCharts, SavedTRangeReferenceCharts }

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
        // chart in its card (see AddTRangeChart). ReferenceLines is T-Range Reference
        // Chart's own addition on top of that -- its third click's line, kept separate
        // from VerticalLines (rather than a third entry there) so it can be coloured and
        // grouped as its own "reference window" instead of reading as part of the main
        // range (see AddReferenceLine/AllVerticalLines/AllRangeFills). Id/RangeBarCount/
        // RangeSpan are only set once a drawing is actually sealed (see FinishDrawing) --
        // 0/null while still _pending, since a range's bar count/span isn't final until
        // its second click lands.
        private sealed record Drawing(
            List<ChartHorizontalLine> HorizontalLines, List<ChartVerticalLine> VerticalLines,
            List<ChartDiagonalLine> DiagonalLines, List<CTraderBarRow> ChartBars, List<ChartVerticalLine> ReferenceLines)
        {
            public int Id { get; init; }
            public int? RangeBarCount { get; init; }
            public TimeSpan? RangeSpan { get; init; }

            // How many of ChartBars' leading bars belong to the reference window rather
            // than the main range -- set once alongside RangeBarCount/RangeSpan (see
            // FinishDrawing), so ChartBars[..ReferenceBarCount] is the reference window
            // and ChartBars[ReferenceBarCount..] is the main range (see CandlestickChart's
            // own ReferenceBarCount parameter, which shades exactly that split).
            public int? ReferenceBarCount { get; init; }

            public string Kind => this switch
            {
                { ChartBars.Count: > 0, ReferenceLines.Count: > 0 } => "T-RANGE REFERENCE CHART",
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
            // always reads in the same colour as the button that placed it. The reference
            // line/window itself is tagged "reference" directly in AllVerticalLines/
            // AllRangeFills instead of through this property, since it needs a colour
            // distinct from its own drawing's main "charts" classification.
            public string? ColorGroup => Kind switch
            {
                "H-LINE" or "V-LINE" or "D-LINE" => "lines",
                "T-RANGE" or "P-RANGE" => "ranges",
                "RECTANGLE" => "shapes",
                "T-RANGE CHART" or "T-RANGE REFERENCE CHART" => "charts",
                _ => null,
            };

            public bool IsRange => VerticalLines.Count == 2 && Kind is "T-RANGE" or "T-RANGE CHART" or "T-RANGE REFERENCE CHART";
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

        // The Volume Range tool's current selection (see DrawableOhlcvChart's
        // DrawMode.VolumeRange/OnVolumeRangeDrawn) -- not a Drawing, just a standalone
        // [start, end] pair that MergedSamples() filters _barHistograms by. Both null
        // means "no selection", which MergedSamples() treats as "use every bar loaded
        // so far" -- the volume profile's original default, still the fallback when
        // this hasn't been set (see SetVolumeRange/ClearVolumeRange below). Reset on a
        // fresh Load (new symbol/range), but -- like _drawings -- kept across LoadMore
        // backfills since it marks a specific time window that's still on screen.
        private DateTime? _volumeRangeStart;
        private DateTime? _volumeRangeEnd;

        // Independent on/off switches for the three computed (non-Drawing) chart
        // overlays -- see the CHART OVERLAYS header in ProfilerPage.razor. Split rather
        // than one shared flag since POC/VA has two separate places it renders (the
        // profile panel itself vs. the projected chart lines) and a user may want just
        // one of those. Not reset on Load -- these are display preferences, not
        // per-session drawing state (same reasoning as _activePanelTab).
        private bool _showProfilePanelLevels = true;
        private bool _showProfileChartLines = true;
        private bool _showVwap = true;

        // Multi-select over _drawings' cards (keyed by Drawing.Id) -- drives the
        // selection bar's LABEL SELECTED/DELETE actions (see ToggleDrawingSelected/
        // RemoveSelectedDrawings/SaveSelectedDrawings below). _savedDrawingIds just
        // marks which cards have been saved at least once this session, for the
        // SAVED badge -- separate from _selectedDrawingIds since a save doesn't
        // clear the card's own selection state. Only T-Range Chart drawings are
        // actually persisted (see SaveSelectedDrawings) -- selecting/removing
        // still works for every kind, same as before.
        private HashSet<int> _selectedDrawingIds = new();
        private HashSet<int> _savedDrawingIds = new();
        private bool _showLabelInput;
        private string _labelText = "";

        // Browsable history of every T-Range Chart labeled via SaveSelectedDrawings,
        // any symbol/session -- independent of the current _config/_drawings, so
        // it's rendered outside the @if (HasData) block (see ProfilerPage.razor).
        private List<TRangeChart> _savedTRangeCharts = new();

        // Same, for T-Range Reference Chart drawings -- its own repository/table (see
        // ITRangeReferenceChartRepository) since every row here always carries a
        // reference window on top of the main range, unlike plain T-Range Chart rows.
        private List<TRangeReferenceChart> _savedTRangeReferenceCharts = new();

        // Bar snapshots for the Saved Drawings table's chart previews, fetched
        // on demand (see GetChartBars/LoadChartBarsAsync) by re-querying the
        // saved symbol's ticks for [StartTimeUtc, EndTimeUtc] rather than storing
        // bars in the database -- keyed by TRangeChart.Id.
        private readonly Dictionary<long, List<CTraderBarRow>> _chartBarsCache = new();
        private readonly HashSet<long> _chartBarsLoading = new();

        // Same, for T-Range Reference Chart -- keyed by TRangeReferenceChart.Id, spanning
        // [ReferenceStartTimeUtc, EndTimeUtc] (reference window + main range together) so
        // the reference area is visible in the redrawn chart too (see
        // GetReferenceChartBars/LoadReferenceChartBarsAsync).
        private readonly Dictionary<long, List<CTraderBarRow>> _referenceChartBarsCache = new();
        private readonly HashSet<long> _referenceChartBarsLoading = new();

        private static Drawing NewPending() => new(new(), new(), new(), new(), new());

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

        protected override void OnInitialized()
        {
            _savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
            _savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
        }

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
            _selectedDrawingIds.Clear();
            _savedDrawingIds.Clear();
            _volumeRangeStart = null;
            _volumeRangeEnd = null;
            CancelLabelSelected();

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
        // click order. For T-Range Reference Chart, drawableOhlcvChart.js fires this only
        // after all 3 clicks have landed (see its 'trangereferencechart' case), so
        // _pending.ReferenceLines is already populated here too -- the slice then starts
        // from the reference click instead of the main range, so the reference window's
        // own bars are part of ChartBars (see FinishDrawing's ReferenceBarCount, which
        // marks where the reference window ends and the main range begins within it).
        private void AddTRangeChart(ChartTimeRange range)
        {
            var start = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
            var end = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
            var sliceStart = _pending.ReferenceLines.Count > 0 ? _pending.ReferenceLines[0].TimeUtc : start;
            var slice = _bars.Where(b => b.OpenTime >= sliceStart && b.OpenTime <= end);
            _pending.ChartBars.AddRange(ToBarRows(slice));
        }

        // Wired to DrawableOhlcvChart's OnReferenceLineDrawn -- T-Range Reference Chart's
        // third click, already guaranteed (by drawableOhlcvChart.js) to land before the
        // earlier of the two main-range clicks that landed first.
        private void AddReferenceLine(DateTime timeUtc) =>
            _pending.ReferenceLines.Add(new ChartVerticalLine(timeUtc, timeUtc.ToString("yyyy-MM-dd HH:mm")));

        // Wired to DrawableOhlcvChart's OnVolumeRangeDrawn -- unlike every other draw
        // tool, this never touches _pending/_drawings (see _volumeRangeStart's doc
        // comment): its two clicks just replace the active selection outright, which is
        // what MergedSamples() filters _barHistograms by. ClearVolumeRange is the only
        // other way these change.
        private void SetVolumeRange(ChartTimeRange range)
        {
            _volumeRangeStart = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
            _volumeRangeEnd = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
        }

        // Wired to the volume profile panel's ALL button (shown only while a selection
        // is active) -- drops back to MergedSamples()'s default of every loaded bar.
        private void ClearVolumeRange()
        {
            _volumeRangeStart = null;
            _volumeRangeEnd = null;
        }

        // Bar count and span of the active Volume Range selection, for
        // TickVolumeProfileChart's RangeLabel -- null while no selection is active,
        // which hides that badge entirely (see _volumeRangeStart).
        private string? VolumeRangeLabel()
        {
            if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end) return null;
            var barCount = _bars.Count(b => b.OpenTime >= start && b.OpenTime <= end);
            return $"{barCount:N0} bars, {FormatSpan(end - start)}";
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

                    // T-Range Reference Chart's reference window is [ReferenceLines[0],
                    // start) -- strictly before the main range's own start, which is also
                    // where AddTRangeChart's ChartBars slice switched from reference bars
                    // to main-range bars, so this count doubles as the split index into
                    // ChartBars that CandlestickChart's ReferenceBarCount parameter needs.
                    if (drawing.ReferenceLines.Count > 0)
                    {
                        var referenceStart = drawing.ReferenceLines[0].TimeUtc;
                        drawing = drawing with
                        {
                            ReferenceBarCount = _bars.Count(b => b.OpenTime >= referenceStart && b.OpenTime < start),
                        };
                    }
                }

                _drawings = _drawings.Append(drawing).ToList();
            }
            _pending = NewPending();
        }

        // Removes a whole Drawing (all of its lines together) -- the side panel gives
        // each card exactly one remove button rather than one per underlying line.
        private void RemoveDrawing(int index)
        {
            _selectedDrawingIds.Remove(_drawings[index].Id);
            _drawings = _drawings.Where((_, i) => i != index).ToList();
        }

        private void ToggleDrawingSelected(int id)
        {
            if (!_selectedDrawingIds.Remove(id))
                _selectedDrawingIds.Add(id);
        }

        // Bulk counterpart to RemoveDrawing above -- local-only, same as the
        // per-card "X" button; doesn't touch anything already saved to the
        // database (see SaveSelectedDrawings below).
        private void RemoveSelectedDrawings()
        {
            _drawings = _drawings.Where(d => !_selectedDrawingIds.Contains(d.Id)).ToList();
            _selectedDrawingIds.Clear();
        }

        private void ShowLabelSelectedInput() => _showLabelInput = true;

        private void CancelLabelSelected()
        {
            _showLabelInput = false;
            _labelText = "";
        }

        private void OnLabelKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter") SaveSelectedDrawings();
            else if (e.Key == "Escape") CancelLabelSelected();
        }

        // Persists every selected T-Range Chart/T-Range Reference Chart drawing's range
        // to the Poink.Sim database under one shared label (see ITRangeChartRepository's
        // and ITRangeReferenceChartRepository's doc comments) -- any other kind among the
        // selection has no well-defined [start, end] range to redraw from later, so it's
        // silently skipped; selection stays on-screen afterwards -- only
        // RemoveSelectedDrawings/the per-card "X" take a drawing off the chart.
        private void SaveSelectedDrawings()
        {
            if (string.IsNullOrWhiteSpace(_labelText) || _config is null || _selectedDrawingIds.Count == 0) return;

            var label = _labelText.Trim();
            foreach (var drawing in _drawings.Where(d =>
                _selectedDrawingIds.Contains(d.Id) && d.Kind is "T-RANGE CHART" or "T-RANGE REFERENCE CHART"))
            {
                var t1 = drawing.VerticalLines[0].TimeUtc;
                var t2 = drawing.VerticalLines[1].TimeUtc;
                var start = t1 <= t2 ? t1 : t2;
                var end = t1 <= t2 ? t2 : t1;
                var barCount = drawing.RangeBarCount ?? drawing.ChartBars.Count - (drawing.ReferenceBarCount ?? 0);

                if (drawing.Kind == "T-RANGE REFERENCE CHART")
                {
                    var referenceStart = drawing.ReferenceLines[0].TimeUtc;
                    var referenceBarCount = drawing.ReferenceBarCount ?? 0;
                    TRangeReferenceChartRepository.SaveTRangeReferenceChart(
                        label, _config.Table, start, end, _config.BarSize.TotalMinutes, barCount,
                        referenceStart, start, referenceBarCount);
                }
                else
                {
                    TRangeChartRepository.SaveTRangeChart(label, _config.Table, start, end, _config.BarSize.TotalMinutes, barCount);
                }

                _savedDrawingIds.Add(drawing.Id);
            }

            _savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
            _savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
            _selectedDrawingIds.Clear();
            CancelLabelSelected();
        }

        private void HandleDeleteTRangeChart(TRangeChart chart)
        {
            TRangeChartRepository.DeleteTRangeChart(chart.Id);
            _savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
            _chartBarsCache.Remove(chart.Id);
        }

        private void HandleDeleteTRangeReferenceChart(TRangeReferenceChart chart)
        {
            TRangeReferenceChartRepository.DeleteTRangeReferenceChart(chart.Id);
            _savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
            _referenceChartBarsCache.Remove(chart.Id);
        }

        // Redraws a saved T-Range Chart by re-querying its symbol's ticks for
        // [StartTimeUtc, EndTimeUtc] rather than reading back a stored bar
        // snapshot (none is stored -- see ITRangeChartRepository's doc comment).
        // Returns null (and kicks off the fetch) the first time a given chart is
        // asked for; cached afterwards. beforeUtc=EndTimeUtc/count=BarCount
        // reproduces the exact same bars the live chart showed when this range
        // was first drawn (see ProfilerService.LoadAsync).
        private List<CTraderBarRow>? GetChartBars(TRangeChart chart)
        {
            if (_chartBarsCache.TryGetValue(chart.Id, out var bars)) return bars;
            if (_chartBarsLoading.Add(chart.Id))
                _ = LoadChartBarsAsync(chart);
            return null;
        }

        private async Task LoadChartBarsAsync(TRangeChart chart)
        {
            try
            {
                var config = new ProfilerConfig(chart.Symbol, chart.StartTimeUtc, chart.EndTimeUtc, TimeSpan.FromMinutes(chart.BarMinutes));
                var loaded = await Service.LoadAsync(config, chart.EndTimeUtc, chart.BarCount);
                _chartBarsCache[chart.Id] = ToBarRows(loaded.Bars);
            }
            finally
            {
                _chartBarsLoading.Remove(chart.Id);
            }
            StateHasChanged();
        }

        // Same as GetChartBars/LoadChartBarsAsync above, but spanning [ReferenceStartTimeUtc,
        // EndTimeUtc] -- reference window + main range together -- so the reference area is
        // visible in the redrawn saved chart too (see CandlestickChart's ReferenceBarCount,
        // fed from the stored chart.ReferenceBarCount directly -- no recomputation needed,
        // it was already derived once when the drawing was first sealed).
        private List<CTraderBarRow>? GetReferenceChartBars(TRangeReferenceChart chart)
        {
            if (_referenceChartBarsCache.TryGetValue(chart.Id, out var bars)) return bars;
            if (_referenceChartBarsLoading.Add(chart.Id))
                _ = LoadReferenceChartBarsAsync(chart);
            return null;
        }

        private async Task LoadReferenceChartBarsAsync(TRangeReferenceChart chart)
        {
            try
            {
                var config = new ProfilerConfig(
                    chart.Symbol, chart.ReferenceStartTimeUtc, chart.EndTimeUtc, TimeSpan.FromMinutes(chart.BarMinutes));
                var loaded = await Service.LoadAsync(config, chart.EndTimeUtc, chart.BarCount + chart.ReferenceBarCount);
                _referenceChartBarsCache[chart.Id] = ToBarRows(loaded.Bars);
            }
            finally
            {
                _referenceChartBarsLoading.Remove(chart.Id);
            }
            StateHasChanged();
        }

        // Flattened for DrawableOhlcvChart, which only ever renders plain lines/shapes/
        // fills and has no notion of a "drawing" grouping -- includes _pending too, so a
        // multi-click tool's lines still appear on the chart as each one is placed, before
        // the group is sealed. Each line is tagged with its owning drawing's ColorGroup so
        // the chart colours it the same as the toolbar button that placed it (see "Show
        // colours on chart respective of type"). New list instances every call (not
        // cached), same reference-equality reasoning ApplyChunk/Add*Line above rely on.
        // POC/VAH/VAL (ProfileLevelLines) are appended the same way -- they aren't a
        // Drawing at all (nothing to select/remove/save), just three computed lines
        // riding the same rendering path as the user-drawn ones.
        private List<ChartHorizontalLine> AllHorizontalLines() =>
            AllDrawings().SelectMany(d => d.HorizontalLines.Select(l => l with { ColorGroup = d.ColorGroup }))
                .Concat(ProfileLevelLines())
                .ToList();

        // Point of Control/Value Area for whatever window MergedSamples() currently
        // builds the volume profile panel from (every loaded bar, or the Volume Range
        // selection if one is active) -- projected onto the main chart so they read
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

        // Reference lines are tagged "reference" here rather than d.ColorGroup ("charts")
        // -- see Drawing.ColorGroup's comment on why the reference window needs its own
        // colour distinct from its drawing's main classification.
        private List<ChartVerticalLine> AllVerticalLines() =>
            AllDrawings()
                .SelectMany(d => d.VerticalLines.Select(l => l with { ColorGroup = d.ColorGroup })
                    .Concat(d.ReferenceLines.Select(l => l with { ColorGroup = "reference" })))
                .ToList();

        private List<ChartDiagonalLine> AllDiagonalLines() =>
            AllDrawings().SelectMany(d => d.DiagonalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

        // One soft translucent band per T-Range/T-Range Chart drawing (see Drawing.IsRange)
        // -- "#Id" labels it on the chart itself, blank while still _pending (Id isn't
        // assigned until FinishDrawing seals it). A T-Range Reference Chart drawing adds
        // a second band of its own, from its reference line back to the earlier of its
        // two main-range lines -- tagged "reference" (not d.ColorGroup) so it renders in
        // a colour distinct from the main range fill above it.
        private List<ChartRangeFill> AllRangeFills() =>
            AllDrawings()
                .Where(d => d.IsRange)
                .Select(d => new ChartRangeFill(
                    d.VerticalLines[0].TimeUtc, d.VerticalLines[1].TimeUtc, d.Id > 0 ? $"#{d.Id}" : null, d.ColorGroup))
                .Concat(AllDrawings()
                    .Where(d => d.ReferenceLines.Count > 0 && d.VerticalLines.Count == 2)
                    .Select(d =>
                    {
                        var earliest = d.VerticalLines[0].TimeUtc <= d.VerticalLines[1].TimeUtc
                            ? d.VerticalLines[0].TimeUtc : d.VerticalLines[1].TimeUtc;
                        return new ChartRangeFill(
                            d.ReferenceLines[0].TimeUtc, earliest, d.Id > 0 ? $"#{d.Id} REF" : null, "reference");
                    }))
                .Concat(VolumeRangeFill())
                .ToList();

        // The Volume Range tool's own highlighted band -- not part of AllDrawings()
        // since it isn't a Drawing at all (see SetVolumeRange above).
        private IEnumerable<ChartRangeFill> VolumeRangeFill()
        {
            if (_volumeRangeStart is { } start && _volumeRangeEnd is { } end)
                yield return new ChartRangeFill(start, end, "VOLUME RANGE", "volume");
        }

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

        // Merges the relevant loaded bars' price histograms into one weighted-sample list
        // for TickVolumeProfileChart -- see BarPriceHistogram.Merge for how bars sharing
        // one BucketWidth get summed bucket-for-bucket instead of needing the original
        // per-tick prices back. Covers every loaded bar by default (grows as more history
        // backfills in, same as ToBarRows above) -- narrowed to _volumeRangeStart/
        // _volumeRangeEnd once the Volume Range tool has set one (see SetVolumeRange),
        // which is the volume profile's originally intended behaviour: a selectable
        // window, not unconditionally everything loaded. _bars and _barHistograms are
        // always the same length and index-aligned (see ApplyChunk/ProfilerService), so
        // zipping them together is safe.
        private List<WeightedPriceSample> MergedSamples()
        {
            var histograms = _barHistograms;
            if (_volumeRangeStart is { } start && _volumeRangeEnd is { } end)
            {
                histograms = _bars
                    .Zip(_barHistograms, (bar, histogram) => (bar, histogram))
                    .Where(t => t.bar.OpenTime >= start && t.bar.OpenTime <= end)
                    .Select(t => t.histogram)
                    .ToList();
            }

            return BarPriceHistogram.Merge(histograms, histograms.Count)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();
        }

        private double? LastClose => _bars.Count > 0 ? _bars[^1].Close : null;
    }
}
