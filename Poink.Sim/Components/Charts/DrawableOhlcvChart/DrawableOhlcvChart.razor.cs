using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Charts
{
    // ColorGroup keys drawableOhlcvChart.js's groupColors map (its own copy of this same
    // classification, read off the matching --color-* CSS custom property) -- "lines",
    // "ranges", "shapes", "charts", matching DrawableOhlcvChart.razor.css's .doc-tool-btn-*
    // groups, or null/unrecognized to fall back to the chart's plain accent colour.
    // Kept as a caller-supplied string rather than a shared enum since only ProfilerPage
    // (via its own Drawing.Kind) knows which classification a given line belongs to --
    // this component just renders whatever colour key it's handed.

    // A horizontal (price) line the user has placed on the chart -- Label is shown in
    // the caller's own line-list UI (see ProfilerPage) and, if set, as the chart's own
    // axis-label title for that price line. Public (not internal), same as
    // TickVolumeProfileChart's WeightedPriceSample -- a public component's [Parameter]
    // can't expose a less-accessible type.
    public readonly record struct ChartHorizontalLine(double Price, string? Label = null, string? ColorGroup = null);

    // A vertical (time) line the user has placed on the chart -- same Label convention
    // as ChartHorizontalLine above (shown in the caller's line list, not on the canvas).
    public readonly record struct ChartVerticalLine(DateTime TimeUtc, string? Label = null, string? ColorGroup = null);

    // A trend line connecting two arbitrary (time, price) points -- unlike the two
    // single-axis line types above, both endpoints matter, so this is its own shape
    // rather than something T-Range/P-Range/Rectangle can be built from.
    public readonly record struct ChartDiagonalLine(
        DateTime StartTimeUtc, double StartPrice, DateTime EndTimeUtc, double EndPrice,
        string? Label = null, string? ColorGroup = null);

    // The two time bounds a T-Range Chart's two clicks land on -- not necessarily
    // ordered (the caller may click the later point first), so consumers should sort
    // before using them as a from/to range (see ProfilerPage's AddTRangeChart).
    public readonly record struct ChartTimeRange(DateTime TimeUtc1, DateTime TimeUtc2);

    // A soft, translucent vertical band spanning [StartUtc, EndUtc] -- how a T-Range/
    // T-Range Chart's span reads as one highlighted region instead of just its two thin
    // vertical lines. Label (typically the drawing's "#Id") is drawn near the top of the
    // band. Not ordered -- consumers should sort Start/End before rendering.
    public readonly record struct ChartRangeFill(DateTime StartUtc, DateTime EndUtc, string? Label = null, string? ColorGroup = null);

    // One point of a computed overlay line (e.g. VWAP) -- unlike ChartHorizontalLine,
    // this is a per-bar time series rather than a single fixed price, so it renders as
    // its own native lightweight-charts LineSeries (see drawableOhlcvChart.js's
    // vwapSeries) instead of the custom vertical/diagonal/range-fill primitives the
    // click-to-draw tools use.
    public readonly record struct ChartLinePoint(DateTime TimeUtc, double Price);

    // One trade-action marker drawn on the candle it happened on -- Shape/ColorGroup
    // are lightweight-charts series-marker primitives ("arrowUp"/"arrowDown"/"circle",
    // see drawableOhlcvChart.js's setActionMarkers), Text is the short label shown
    // under/over the marker ("L"/"S"/"C"). See TrainLiveTab's episode replay, which is
    // the only current producer -- one per EnterLong/EnterShort/Close decision bar
    // (Hold isn't marked, it's the majority of bars and would just be clutter).
    public readonly record struct ChartMarker(DateTime TimeUtc, string Shape, string ColorGroup, string? Text = null);

    // Candlestick + volume chart with click-to-draw annotations -- horizontal price
    // lines, vertical time lines, diagonal trend lines, plus T-Range/P-Range/Rectangle
    // (built from placing 2 or 2+2 of the single-axis lines in one go, see DrawMode) --
    // plus the same pan-triggered "near the oldest loaded bar" backfill hook
    // ProfilerPage's lazy loading needs (OnNearStart/KeepView), plus SimulationPage's
    // live-trading overlay (CurrentBid/CurrentAsk, the selected trader's open-position
    // lines, and the tick/bar window-start marker below).
    //
    // One shared component/JS module across both use cases rather than two -- a caller
    // that doesn't need a given group of parameters (Profiler never sets CurrentBid/
    // EntryPrice; Simulation never wires the draw-tool callbacks) just leaves them at
    // their defaults, since none of the groups depend on each other.
    public partial class DrawableOhlcvChart : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public List<CTraderBarRow> Bars { get; set; } = new();

        // Live bid/ask, as horizontal price lines pinned to the right axis -- see
        // SimulationPage's sim-header price readout, which drives these every tick.
        [Parameter] public double? CurrentBid { get; set; }
        [Parameter] public double? CurrentAsk { get; set; }

        // The selected trader's open position: entry price (solid LONG/SHORT line) plus
        // dashed SL/TP lines -- each null independently hides just that one line (flat,
        // or a position missing that field). See SimulationPage.SelectedPositionIsLong.
        [Parameter] public double? EntryPrice { get; set; }
        [Parameter] public bool? PositionIsLong { get; set; }
        [Parameter] public double? StopLossPrice { get; set; }
        [Parameter] public double? TakeProfitPrice { get; set; }

        // Timestamp the tick/bar window (feeding SimulationPage's volume-profile panel)
        // starts at -- null hides the marker (e.g. once the whole buffer fits inside the
        // window). See SimulationPage.WindowStartUtc.
        [Parameter] public DateTime? WindowStartUtc { get; set; }

        // Skips the auto fit-to-content/rezoom a Bars update normally triggers -- the
        // caller sets this true once the initial chunk has been drawn, so a later
        // backfill (prepending older bars) doesn't reset the user's current pan/zoom.
        [Parameter] public bool KeepView { get; set; }

        // Fires when the user pans/zooms close to the oldest bar currently loaded, so
        // the caller can fetch and prepend the next older chunk.
        [Parameter] public EventCallback OnNearStart { get; set; }

        [Parameter] public IReadOnlyList<ChartHorizontalLine> HorizontalLines { get; set; } = Array.Empty<ChartHorizontalLine>();
        [Parameter] public IReadOnlyList<ChartVerticalLine> VerticalLines { get; set; } = Array.Empty<ChartVerticalLine>();
        [Parameter] public IReadOnlyList<ChartDiagonalLine> DiagonalLines { get; set; } = Array.Empty<ChartDiagonalLine>();
        [Parameter] public IReadOnlyList<ChartRangeFill> RangeFills { get; set; } = Array.Empty<ChartRangeFill>();

        // Trade-action markers drawn directly on the candles (see ChartMarker) --
        // its own parameter rather than folded into DiagonalLines/HorizontalLines
        // since it renders through lightweight-charts' native series-markers plugin,
        // not the hand-rolled canvas primitives those use.
        [Parameter] public IReadOnlyList<ChartMarker> Markers { get; set; } = Array.Empty<ChartMarker>();

        // A computed overlay line (e.g. VWAP) drawn across the same time axis as Bars --
        // not a user-drawn annotation, so it has no OnXxxDrawn callback of its own; the
        // caller recomputes it from whatever bars/window it wants (see ProfilerPage's
        // VwapPoints) and hands the whole series down each render.
        [Parameter] public IReadOnlyList<ChartLinePoint> VwapLine { get; set; } = Array.Empty<ChartLinePoint>();

        // Fired with the clicked price/time once the user places a line while a draw
        // mode is armed -- the caller owns the canonical HorizontalLines/VerticalLines/
        // DiagonalLines lists (see ProfilerPage) and is expected to append to them and
        // pass the updated list back down; this component only ever reports what was
        // clicked. T-Range/P-Range/Rectangle need no callbacks of their own -- see the
        // JS-side click dispatch in drawableOhlcvChart.js, which places them by firing
        // OnHorizontalLineDrawn/OnVerticalLineDrawn multiple times (2 or 2+2) instead.
        [Parameter] public EventCallback<double> OnHorizontalLineDrawn { get; set; }
        [Parameter] public EventCallback<DateTime> OnVerticalLineDrawn { get; set; }
        [Parameter] public EventCallback<ChartDiagonalLine> OnDiagonalLineDrawn { get; set; }

        // Fired once T-Range Chart's two time clicks have both landed -- alongside (not
        // instead of) the same two OnVerticalLineDrawn calls T-Range itself would fire,
        // so the caller gets both the two vertical lines on the main chart *and* the
        // bounds for whatever "small version of the price range" it wants to build
        // (see ProfilerPage's AddTRangeChart, which slices its loaded bars by this range
        // into a mini candlestick chart). T-Range Reference Chart fires this too, for
        // its own first two clicks.
        [Parameter] public EventCallback<ChartTimeRange> OnTRangeChartDrawn { get; set; }

        // Fired once T-Range Reference Chart's third click lands -- kept separate from
        // OnVerticalLineDrawn (rather than just a third vertical line) so the caller can
        // colour and group it as its own "reference window" rather than folding it into
        // the main range's two lines (see ProfilerPage's AddReferenceLine/ReferenceLines).
        // drawableOhlcvChart.js already rejects (ignores) any click here that doesn't
        // land before the earlier of the first two, so whatever reaches this callback is
        // always a valid reference point.
        [Parameter] public EventCallback<DateTime> OnReferenceLineDrawn { get; set; }

        // Fired once Volume Range's two time clicks have both landed. Deliberately does
        // NOT also fire OnVerticalLineDrawn for either click (unlike TRange/TRangeChart) --
        // this range isn't a chart annotation the caller should fold into its own
        // drawings list, it's a standalone selection whose only job is telling the caller
        // (ProfilerPage) which time window to build the volume profile panel from instead
        // of every bar loaded so far.
        [Parameter] public EventCallback<ChartTimeRange> OnVolumeRangeDrawn { get; set; }

        // True once the caller has a Volume Range selection set (see OnVolumeRangeDrawn) --
        // drives the VOLUME RANGE toolbar button's own clear state (see ToggleVolumeRange),
        // since this component doesn't retain the selection itself once it's been reported.
        [Parameter] public bool HasVolumeRange { get; set; }

        // Fired when the VOLUME RANGE toolbar button is clicked while HasVolumeRange is
        // true and the tool isn't currently armed -- see ToggleVolumeRange.
        [Parameter] public EventCallback OnVolumeRangeCleared { get; set; }

        // Fired once an armed mode's whole click sequence is done (whether that's one
        // click, Diagonal's two, or T-Range/P-Range/Rectangle's 2/2/4) -- lets the caller
        // know where one "drawing" ends, e.g. to group a multi-click tool's several
        // OnHorizontalLineDrawn/OnVerticalLineDrawn calls into one removable card
        // (see ProfilerPage's Drawing/_pending).
        [Parameter] public EventCallback OnDrawFinished { get; set; }

        // None/Horizontal/Vertical/Diagonal place one shape from one chart click (or two,
        // for Diagonal's two endpoints -- collected client-side, see drawableOhlcvChart.js's
        // drawClicks). TRange/PRange/Rectangle are pure UI conveniences built from the same
        // underlying H-line/V-line placement: TRange = 2 vertical clicks, PRange = 2
        // horizontal clicks, Rectangle = 2 vertical + 2 horizontal, dispatched as repeated
        // OnHorizontalLineDrawn/OnVerticalLineDrawn calls rather than a shape of their own.
        // TRangeChart is TRange plus one extra OnTRangeChartDrawn call once both clicks
        // land -- its own classification (see ProfilerPage's Drawing.Kind), not merely
        // TRange with a flag, since it produces its own kind of card in the side panel.
        // TRangeReferenceChart is TRangeChart plus a third click for a "reference window"
        // line, constrained (client-side, see drawableOhlcvChart.js) to land before the
        // earlier of the first two.
        // VolumeRange is its own thing, not built from H-line/V-line placement at all --
        // its two clicks never dispatch OnVerticalLineDrawn, only the bundled
        // OnVolumeRangeDrawn once both land (see that callback's doc comment), since its
        // whole purpose is telling the caller which time window to build the volume
        // profile from rather than adding an annotation to the chart.
        private enum DrawMode { None, Horizontal, Vertical, Diagonal, TRange, PRange, Rectangle, TRangeChart, TRangeReferenceChart, VolumeRange }
        private DrawMode _mode;

        // "What to click next" text for the toolbar hint span -- kept up to date by
        // drawableOhlcvChart.js (see OnDrawHintAsync) rather than derived here, since only
        // its click state machine knows how many clicks have landed so far for the armed
        // mode (including Diagonal's first click, which produces no line/callback this
        // component could otherwise derive progress from). Null while no mode is armed.
        private string? _drawHint;

        private ElementReference _containerRef;
        private ElementReference _minimapRef;
        private IJSObjectReference? _module;
        private string? _chartId;
        private DotNetObjectReference<DrawableOhlcvChart>? _dotNetRef;
        private List<CTraderBarRow>? _renderedBars;
        private IReadOnlyList<ChartHorizontalLine>? _renderedHorizontalLines;
        private IReadOnlyList<ChartVerticalLine>? _renderedVerticalLines;
        private IReadOnlyList<ChartDiagonalLine>? _renderedDiagonalLines;
        private IReadOnlyList<ChartRangeFill>? _renderedRangeFills;
        private IReadOnlyList<ChartMarker>? _renderedMarkers;
        private IReadOnlyList<ChartLinePoint>? _renderedVwapLine;
        private double? _renderedBid;
        private double? _renderedAsk;
        private double? _renderedEntryPrice;
        private bool? _renderedPositionIsLong;
        private double? _renderedStopLoss;
        private double? _renderedTakeProfit;
        private DateTime? _renderedWindowStart;
        private bool _expanded;
        private bool _disposed;

        private static string JsDrawMode(DrawMode mode) => mode switch
        {
            DrawMode.Horizontal => "horizontal",
            DrawMode.Vertical => "vertical",
            DrawMode.Diagonal => "diagonal",
            DrawMode.TRange => "trange",
            DrawMode.PRange => "prange",
            DrawMode.Rectangle => "rectangle",
            DrawMode.TRangeChart => "trangechart",
            DrawMode.TRangeReferenceChart => "trangereferencechart",
            DrawMode.VolumeRange => "volumerange",
            _ => "",
        };

        // Toggling the same mode again disarms it -- a quick way to cancel a placement
        // you no longer want without needing a separate "cancel" control. drawableOhlcvChart.js
        // resets any in-progress multi-click sequence (e.g. Diagonal/Rectangle's first
        // click) whenever the mode changes, so switching tools mid-placement is also safe.
        private async Task ToggleMode(DrawMode mode)
        {
            _mode = _mode == mode ? DrawMode.None : mode;

            if (_module is null || _chartId is null) return;
            try
            {
                await _module.InvokeVoidAsync(
                    "setDrawMode", _chartId, _mode == DrawMode.None ? null : JsDrawMode(_mode));
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }

        // VOLUME RANGE's button triples as arm/disarm/clear instead of plain arm/disarm
        // like every other tool's ToggleMode: clicking while it's currently armed just
        // disarms it (same as any tool); clicking while unarmed but a selection already
        // exists (HasVolumeRange) clears that selection via OnVolumeRangeCleared instead
        // of re-arming, since the button visibly reads as "a range is selected" rather
        // than "ready to draw" at that point (see the .razor markup's label/title
        // swap) -- drawing a replacement range is still one click away via CLEAR then
        // VOLUME RANGE again. Only unarmed with no selection falls through to the normal
        // arm behaviour.
        private async Task ToggleVolumeRange()
        {
            if (_mode == DrawMode.VolumeRange)
            {
                await ToggleMode(DrawMode.VolumeRange);
                return;
            }

            if (HasVolumeRange)
            {
                if (OnVolumeRangeCleared.HasDelegate)
                    await OnVolumeRangeCleared.InvokeAsync();
                return;
            }

            await ToggleMode(DrawMode.VolumeRange);
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // See OhlcvChart.razor.cs's OnAfterRenderAsync for why _disposed is checked
            // after every await -- same reasoning applies here.
            try
            {
                if (firstRender)
                {
                    _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/drawableOhlcvChart.js");
                    if (_disposed) return;
                    _dotNetRef = DotNetObjectReference.Create(this);
                    _chartId = await _module.InvokeAsync<string>("create", _containerRef, _minimapRef, _dotNetRef);
                }

                if (_disposed) return;

                if (_module is not null && _chartId is not null && !ReferenceEquals(_renderedBars, Bars))
                {
                    _renderedBars = Bars;
                    var payload = Bars.Select(b => new
                    {
                        time = new DateTimeOffset(b.TimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        open = b.Open,
                        high = b.High,
                        low = b.Low,
                        close = b.Close,
                        volume = b.Volume,
                    });
                    await _module.InvokeVoidAsync("setData", _chartId, payload, !KeepView);
                }

                if (_disposed) return;

                if (_module is not null && _chartId is not null && !ReferenceEquals(_renderedVwapLine, VwapLine))
                {
                    _renderedVwapLine = VwapLine;
                    var vwapPoints = VwapLine.Select(p =>
                        new { time = new DateTimeOffset(p.TimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(), price = p.Price });
                    await _module.InvokeVoidAsync("setVwapLine", _chartId, vwapPoints);
                }

                if (_disposed) return;

                if (_module is not null && _chartId is not null &&
                    (!ReferenceEquals(_renderedHorizontalLines, HorizontalLines) ||
                     !ReferenceEquals(_renderedVerticalLines, VerticalLines) ||
                     !ReferenceEquals(_renderedDiagonalLines, DiagonalLines) ||
                     !ReferenceEquals(_renderedRangeFills, RangeFills)))
                {
                    _renderedHorizontalLines = HorizontalLines;
                    _renderedVerticalLines = VerticalLines;
                    _renderedDiagonalLines = DiagonalLines;
                    _renderedRangeFills = RangeFills;

                    var hLines = HorizontalLines.Select(l => new { price = l.Price, label = l.Label, colorGroup = l.ColorGroup });
                    var vLines = VerticalLines.Select(l => new
                    { time = new DateTimeOffset(l.TimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(), colorGroup = l.ColorGroup });
                    var dLines = DiagonalLines.Select(l => new
                    {
                        time1 = new DateTimeOffset(l.StartTimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        price1 = l.StartPrice,
                        time2 = new DateTimeOffset(l.EndTimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        price2 = l.EndPrice,
                        colorGroup = l.ColorGroup,
                    });
                    var fills = RangeFills.Select(f => new
                    {
                        time1 = new DateTimeOffset(f.StartUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        time2 = new DateTimeOffset(f.EndUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        label = f.Label,
                        colorGroup = f.ColorGroup,
                    });

                    await _module.InvokeVoidAsync("setLines", _chartId, hLines, vLines, dLines, fills);
                }

                if (_disposed) return;

                if (_module is not null && _chartId is not null && !ReferenceEquals(_renderedMarkers, Markers))
                {
                    _renderedMarkers = Markers;
                    var markers = Markers.Select(m => new
                    {
                        time = new DateTimeOffset(m.TimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        shape = m.Shape,
                        colorGroup = m.ColorGroup,
                        text = m.Text,
                    });
                    await _module.InvokeVoidAsync("setActionMarkers", _chartId, markers);
                }

                if (_disposed) return;

                // Gated separately from Bars: CurrentBid/Ask moves every tick, while Bars
                // only advances once per completed bar interval (see OhlcvChart's original
                // reasoning for the same split).
                if (_module is not null && _chartId is not null && (_renderedBid != CurrentBid || _renderedAsk != CurrentAsk))
                {
                    _renderedBid = CurrentBid;
                    _renderedAsk = CurrentAsk;
                    await _module.InvokeVoidAsync("setCurrentPrices", _chartId, CurrentBid, CurrentAsk);
                }

                if (_disposed) return;

                // Only moves when the position opens/closes/changes, not every tick.
                if (_module is not null && _chartId is not null &&
                    (_renderedEntryPrice != EntryPrice || _renderedPositionIsLong != PositionIsLong ||
                     _renderedStopLoss != StopLossPrice || _renderedTakeProfit != TakeProfitPrice))
                {
                    _renderedEntryPrice = EntryPrice;
                    _renderedPositionIsLong = PositionIsLong;
                    _renderedStopLoss = StopLossPrice;
                    _renderedTakeProfit = TakeProfitPrice;
                    await _module.InvokeVoidAsync("setPositionLevels", _chartId, EntryPrice, PositionIsLong, StopLossPrice, TakeProfitPrice);
                }

                if (_disposed) return;

                if (_module is not null && _chartId is not null && _renderedWindowStart != WindowStartUtc)
                {
                    _renderedWindowStart = WindowStartUtc;
                    long? unixSeconds = WindowStartUtc is { } w ? new DateTimeOffset(w, TimeSpan.Zero).ToUnixTimeSeconds() : null;
                    await _module.InvokeVoidAsync("setWindowStart", _chartId, unixSeconds);
                }
            }
            catch (ObjectDisposedException) { }
            catch (JSDisconnectedException) { }
        }

        private void ToggleExpanded() => _expanded = !_expanded;

        // Invoked from drawableOhlcvChart.js's subscribeVisibleLogicalRangeChange
        // handler once per approach to the start of the loaded data -- forwards to
        // OnNearStart, then tells the JS side it's free to fire again once this round
        // trip (whether it actually loaded more or not) has finished.
        [JSInvokable]
        public async Task OnNearStartAsync()
        {
            if (OnNearStart.HasDelegate)
                await OnNearStart.InvokeAsync();

            if (_disposed || _module is null || _chartId is null) return;
            try { await _module.InvokeVoidAsync("resumeLoadMore", _chartId); }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }

        // Invoked from drawableOhlcvChart.js once a chart click completes a horizontal
        // line (H-Line's single click, or one of P-Range/Rectangle's price clicks).
        [JSInvokable]
        public Task OnHorizontalLineClickedAsync(double price) => InvokeIfBound(OnHorizontalLineDrawn, price);

        // Same as above for a vertical line (V-Line's single click, or one of T-Range/
        // Rectangle's time clicks).
        [JSInvokable]
        public Task OnVerticalLineClickedAsync(double unixSeconds) =>
            InvokeIfBound(OnVerticalLineDrawn, FromUnixSeconds(unixSeconds));

        // Invoked once Diagonal's two endpoint clicks have both landed.
        [JSInvokable]
        public Task OnDiagonalLineClickedAsync(double price1, double unixSeconds1, double price2, double unixSeconds2) =>
            InvokeIfBound(OnDiagonalLineDrawn,
                new ChartDiagonalLine(FromUnixSeconds(unixSeconds1), price1, FromUnixSeconds(unixSeconds2), price2));

        // Invoked once T-Range Chart's two time clicks have both landed -- fired in
        // addition to the two OnVerticalLineClickedAsync calls drawableOhlcvChart.js
        // already sent for those same two clicks (see its 'trangechart' case).
        [JSInvokable]
        public Task OnTRangeChartClickedAsync(double unixSeconds1, double unixSeconds2) =>
            InvokeIfBound(OnTRangeChartDrawn, new ChartTimeRange(FromUnixSeconds(unixSeconds1), FromUnixSeconds(unixSeconds2)));

        // Invoked once T-Range Reference Chart's third click lands -- drawableOhlcvChart.js
        // only ever sends one that already passed its "before the earlier of the first
        // two" check, so no further validation is needed here.
        [JSInvokable]
        public Task OnReferenceLineClickedAsync(double unixSeconds) =>
            InvokeIfBound(OnReferenceLineDrawn, FromUnixSeconds(unixSeconds));

        // Invoked once Volume Range's two time clicks have both landed -- see
        // OnVolumeRangeDrawn's doc comment for why this stays independent of
        // OnVerticalLineClickedAsync/OnTRangeChartClickedAsync.
        [JSInvokable]
        public Task OnVolumeRangeClickedAsync(double unixSeconds1, double unixSeconds2) =>
            InvokeIfBound(OnVolumeRangeDrawn, new ChartTimeRange(FromUnixSeconds(unixSeconds1), FromUnixSeconds(unixSeconds2)));

        // Invoked by drawableOhlcvChart.js whenever the toolbar's "click the chart to
        // place it" hint should change -- arming a tool (its first click's instructions),
        // each click that lands (the next click's instructions), or a rejected click (e.g.
        // T-Range Reference Chart's 3rd click landing on/after the earlier of its first
        // two) explaining why it didn't count. Null clears it (tool disarmed).
        [JSInvokable]
        public Task OnDrawHintAsync(string? hint)
        {
            _drawHint = hint;
            StateHasChanged();
            return Task.CompletedTask;
        }

        // Invoked once a draw mode's full click sequence has been dispatched (whether
        // that's H-Line/V-Line's one click, Diagonal's two, or T-Range/P-Range/Rectangle's
        // 2/2/4) -- separate from the per-line callbacks above so the toolbar's active
        // button stays highlighted through a multi-click placement and only resets once
        // the whole shape is actually done.
        [JSInvokable]
        public async Task OnDrawFinishedAsync()
        {
            _mode = DrawMode.None;
            _drawHint = null;
            StateHasChanged();

            if (OnDrawFinished.HasDelegate)
                await OnDrawFinished.InvokeAsync();
        }

        private static Task InvokeIfBound<T>(EventCallback<T> callback, T value) =>
            callback.HasDelegate ? callback.InvokeAsync(value) : Task.CompletedTask;

        private static DateTime FromUnixSeconds(double unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds((long)unixSeconds).UtcDateTime;

        public async ValueTask DisposeAsync()
        {
            _disposed = true;

            if (_module is not null)
            {
                try
                {
                    if (_chartId is not null) await _module.InvokeVoidAsync("dispose", _chartId);
                    await _module.DisposeAsync();
                }
                catch (JSDisconnectedException) { }
                catch (ObjectDisposedException) { }
            }

            _dotNetRef?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
