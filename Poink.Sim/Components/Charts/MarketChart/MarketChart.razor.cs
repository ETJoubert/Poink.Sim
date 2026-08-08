using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Charts
{
    // ColorGroup keys marketChart.js's own groupColors map; null/unrecognized falls back to the plain accent colour.
    public readonly record struct ChartHorizontalLine(double Price, string? Label = null, string? ColorGroup = null);

    public readonly record struct ChartVerticalLine(DateTime TimeUtc, string? Label = null, string? ColorGroup = null);

    // Both endpoints matter, so this isn't built from the two single-axis line types above.
    public readonly record struct ChartDiagonalLine(
        DateTime StartTimeUtc, double StartPrice, DateTime EndTimeUtc, double EndPrice,
        string? Label = null, string? ColorGroup = null);

    // Not necessarily ordered -- caller may click the later point first.
    public readonly record struct ChartTimeRange(DateTime TimeUtc1, DateTime TimeUtc2);

    // Translucent vertical band over [StartUtc, EndUtc]; not ordered, sort before rendering.
    public readonly record struct ChartRangeFill(DateTime StartUtc, DateTime EndUtc, string? Label = null, string? ColorGroup = null);

    // A computed overlay series (e.g. VWAP), rendered as its own lightweight-charts LineSeries.
    public readonly record struct ChartLinePoint(DateTime TimeUtc, double Price);

    // Shape/ColorGroup are lightweight-charts series-marker primitives; Text is the short label under/over the marker.
    public readonly record struct ChartMarker(DateTime TimeUtc, string Shape, string ColorGroup, string? Text = null);

    // Candlestick + volume chart with click-to-draw annotations, shared by ProfilerPage and SimulationPage's live overlay.
    public partial class MarketChart : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public List<CTraderBarRow> Bars { get; set; } = new();

        // Hides the Lines/Ranges/Shapes/Charts toolbar groups, leaving only Volume Range -- see TransformerPage.
        [Parameter] public bool ShowAnnotationTools { get; set; } = true;

        // False hides the whole toolbar and disables pan/zoom/scroll/minimap-drag -- for read-only,
        // frequently-updating views like EpisodePlayback's live episode chart, where letting the user
        // interact while Bars keeps changing underneath them can crash lightweight-charts internally.
        [Parameter] public bool Interactive { get; set; } = true;

        // Live bid/ask pinned to the right axis -- see SimulationPage's sim-header price readout.
        [Parameter] public double? CurrentBid { get; set; }
        [Parameter] public double? CurrentAsk { get; set; }

        // Open-position lines; each null independently hides just that line.
        [Parameter] public double? EntryPrice { get; set; }
        [Parameter] public bool? PositionIsLong { get; set; }
        [Parameter] public double? StopLossPrice { get; set; }
        [Parameter] public double? TakeProfitPrice { get; set; }

        // Tick/bar window-start marker; null hides it.
        [Parameter] public DateTime? WindowStartUtc { get; set; }

        // Skips the auto fit-to-content a Bars update normally triggers, so backfill doesn't reset pan/zoom.
        [Parameter] public bool KeepView { get; set; }

        // Fires when panning/zooming nears the oldest loaded bar, so the caller can prepend an older chunk.
        [Parameter] public EventCallback OnNearStart { get; set; }

        [Parameter] public IReadOnlyList<ChartHorizontalLine> HorizontalLines { get; set; } = Array.Empty<ChartHorizontalLine>();
        [Parameter] public IReadOnlyList<ChartVerticalLine> VerticalLines { get; set; } = Array.Empty<ChartVerticalLine>();
        [Parameter] public IReadOnlyList<ChartDiagonalLine> DiagonalLines { get; set; } = Array.Empty<ChartDiagonalLine>();
        [Parameter] public IReadOnlyList<ChartRangeFill> RangeFills { get; set; } = Array.Empty<ChartRangeFill>();

        [Parameter] public IReadOnlyList<ChartMarker> Markers { get; set; } = Array.Empty<ChartMarker>();

        [Parameter] public IReadOnlyList<ChartLinePoint> VwapLine { get; set; } = Array.Empty<ChartLinePoint>();

        // Caller owns the canonical lines lists and is expected to append + pass the updated list back down.
        [Parameter] public EventCallback<double> OnHorizontalLineDrawn { get; set; }
        [Parameter] public EventCallback<DateTime> OnVerticalLineDrawn { get; set; }
        [Parameter] public EventCallback<ChartDiagonalLine> OnDiagonalLineDrawn { get; set; }

        // Fires once T-Range Chart's two clicks land, alongside the matching OnVerticalLineDrawn calls.
        [Parameter] public EventCallback<ChartTimeRange> OnTRangeChartDrawn { get; set; }

        // Fires once T-Range Reference Chart's third click lands -- kept separate so the caller can group/colour it as its own reference window.
        [Parameter] public EventCallback<DateTime> OnReferenceLineDrawn { get; set; }

        // Fires once Volume Range's two clicks land, and again on move/resize -- deliberately does NOT also fire OnVerticalLineDrawn.
        [Parameter] public EventCallback<ChartTimeRange> OnVolumeRangeDrawn { get; set; }

        // True once the caller has a Volume Range selection -- drives the toolbar button's clear state.
        [Parameter] public bool HasVolumeRange { get; set; }

        [Parameter] public EventCallback OnVolumeRangeCleared { get; set; }

        // Fires once an armed mode's whole click sequence is done, so the caller can group multi-click drawings into one card.
        [Parameter] public EventCallback OnDrawFinished { get; set; }

        // TRange/PRange/Rectangle are built from repeated H/V-line placement, not their own shape; VolumeRange is unrelated to line placement entirely.
        private enum DrawMode { None, Horizontal, Vertical, Diagonal, TRange, PRange, Rectangle, TRangeChart, TRangeReferenceChart, VolumeRange }
        private DrawMode _mode;

        // Kept up to date by marketChart.js, which owns the click state machine. Null while no mode is armed.
        private string? _drawHint;

        private ElementReference _containerRef;
        private ElementReference _minimapRef;
        private IJSObjectReference? _module;
        private string? _chartId;
        private DotNetObjectReference<MarketChart>? _dotNetRef;
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

        // Toggling the same mode again disarms it.
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

        // Triples as arm/disarm/clear: armed -> disarm; unarmed with a selection -> clear via OnVolumeRangeCleared; otherwise arm.
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
            // See OhlcvChart.razor.cs's OnAfterRenderAsync for why _disposed is checked after every await.
            try
            {
                if (firstRender)
                {
                    _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/marketChart.js");
                    if (_disposed) return;
                    _dotNetRef = DotNetObjectReference.Create(this);
                    _chartId = await _module.InvokeAsync<string>("create", _containerRef, _minimapRef, _dotNetRef, Interactive);
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

                // Gated separately from Bars: CurrentBid/Ask moves every tick, Bars only once per completed bar.
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

        // Forwards to OnNearStart, then tells JS it's free to fire again once this round trip finishes.
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

        [JSInvokable]
        public Task OnHorizontalLineClickedAsync(double price) => InvokeIfBound(OnHorizontalLineDrawn, price);

        [JSInvokable]
        public Task OnVerticalLineClickedAsync(double unixSeconds) =>
            InvokeIfBound(OnVerticalLineDrawn, FromUnixSeconds(unixSeconds));

        [JSInvokable]
        public Task OnDiagonalLineClickedAsync(double price1, double unixSeconds1, double price2, double unixSeconds2) =>
            InvokeIfBound(OnDiagonalLineDrawn,
                new ChartDiagonalLine(FromUnixSeconds(unixSeconds1), price1, FromUnixSeconds(unixSeconds2), price2));

        // Fired in addition to the two OnVerticalLineClickedAsync calls already sent for these same two clicks.
        [JSInvokable]
        public Task OnTRangeChartClickedAsync(double unixSeconds1, double unixSeconds2) =>
            InvokeIfBound(OnTRangeChartDrawn, new ChartTimeRange(FromUnixSeconds(unixSeconds1), FromUnixSeconds(unixSeconds2)));

        [JSInvokable]
        public Task OnReferenceLineClickedAsync(double unixSeconds) =>
            InvokeIfBound(OnReferenceLineDrawn, FromUnixSeconds(unixSeconds));

        // Also fired on move/resize of an already-placed band, not just the initial two clicks.
        [JSInvokable]
        public Task OnVolumeRangeClickedAsync(double unixSeconds1, double unixSeconds2) =>
            InvokeIfBound(OnVolumeRangeDrawn, new ChartTimeRange(FromUnixSeconds(unixSeconds1), FromUnixSeconds(unixSeconds2)));

        // Null clears the hint (tool disarmed).
        [JSInvokable]
        public Task OnDrawHintAsync(string? hint)
        {
            _drawHint = hint;
            StateHasChanged();
            return Task.CompletedTask;
        }

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
