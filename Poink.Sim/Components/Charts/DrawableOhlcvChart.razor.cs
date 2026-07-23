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

    // Candlestick + volume chart with click-to-draw annotations -- horizontal price
    // lines, vertical time lines, diagonal trend lines, plus T-Range/P-Range/Rectangle
    // (built from placing 2 or 2+2 of the single-axis lines in one go, see DrawMode) --
    // plus the same pan-triggered "near the oldest loaded bar" backfill hook
    // ProfilerPage's lazy loading needs (OnNearStart/KeepView).
    //
    // Deliberately a separate component and JS module from OhlcvChart, not a wrapper
    // around it -- OhlcvChart has no hook for click events or externally-supplied chart
    // primitives, and Simulation (its only other caller) has no need for either, so
    // adding them there would be dead weight on a component doing fine as-is. Profiler's
    // chart needs have diverged enough (drawing tools, lazy history) to justify the small
    // amount of duplicated chart-setup code between the two JS modules instead.
    public partial class DrawableOhlcvChart : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public List<CTraderBarRow> Bars { get; set; } = new();

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
        // into a mini candlestick chart).
        [Parameter] public EventCallback<ChartTimeRange> OnTRangeChartDrawn { get; set; }

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
        private enum DrawMode { None, Horizontal, Vertical, Diagonal, TRange, PRange, Rectangle, TRangeChart }
        private DrawMode _mode;

        private ElementReference _containerRef;
        private IJSObjectReference? _module;
        private string? _chartId;
        private DotNetObjectReference<DrawableOhlcvChart>? _dotNetRef;
        private List<CTraderBarRow>? _renderedBars;
        private IReadOnlyList<ChartHorizontalLine>? _renderedHorizontalLines;
        private IReadOnlyList<ChartVerticalLine>? _renderedVerticalLines;
        private IReadOnlyList<ChartDiagonalLine>? _renderedDiagonalLines;
        private IReadOnlyList<ChartRangeFill>? _renderedRangeFills;
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
                    _chartId = await _module.InvokeAsync<string>("create", _containerRef, _dotNetRef);
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

        // Invoked once a draw mode's full click sequence has been dispatched (whether
        // that's H-Line/V-Line's one click, Diagonal's two, or T-Range/P-Range/Rectangle's
        // 2/2/4) -- separate from the per-line callbacks above so the toolbar's active
        // button stays highlighted through a multi-click placement and only resets once
        // the whole shape is actually done.
        [JSInvokable]
        public async Task OnDrawFinishedAsync()
        {
            _mode = DrawMode.None;
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
