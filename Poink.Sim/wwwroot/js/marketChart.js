// Candlestick + volume chart shared by ProfilerPage/EpisodePlayback and SimulationPage:
// click-to-draw horizontal/vertical/diagonal lines (plus T-Range/P-Range/Rectangle,
// built from placing 2 or 2+2 of the single-axis lines -- see modeClickCounts), pan-
// triggered history backfill, and Simulation's live bid/ask/position/window-start
// overlay (see MarketChart.razor.cs's class comment).
const instances = new Map();

const upColor = '#4dff88';
const downColor = '#ff5f56';

// How close (in bars) the visible left edge has to get to the start of the
// currently loaded data before a backfill request fires -- kept generous (well
// above lightweight-charts' own infinite-history examples, which use ~10) so the
// next chunk's round trip finishes before the user actually pans/zooms to the
// loaded edge, instead of only starting once they're already there.
const nearStartThreshold = 50;

// Bars of empty space left of the oldest loaded bar on a fresh fitContent(), mirroring
// the timeScale's own rightOffset -- without it the first bar sits flush against the
// left edge, under the pane's edge chrome.
const leftOffsetBars = 10;

// Hand-rolled Series Primitive drawing an arbitrary set of dashed vertical lines, each
// in its own colour -- same technique ohlcvChart.js's single-purpose window-start
// marker uses (updateAllViews()/requestUpdate(), called by the chart right before every
// paint), generalized to a list (each carrying its own {time, color}) since Profiler
// can have any number of drawn time markers across different classifications (see
// setLines' colorFor -- lines/ranges/shapes/charts each get their own colour).
function createVerticalLinesPrimitive() {
    let lines = []; // { time, color }
    let chart = null;
    let requestUpdate = null;

    const renderer = {
        draw(target) {
            if (lines.length === 0 || !chart) return;
            target.useBitmapCoordinateSpace(scope => {
                const ctx = scope.context;
                ctx.save();
                ctx.lineWidth = 1;
                ctx.setLineDash([4, 4]);
                for (const line of lines) {
                    const x = chart.timeScale().timeToCoordinate(line.time);
                    if (x === null) continue;
                    const px = Math.round(x * scope.horizontalPixelRatio) + 0.5;
                    ctx.strokeStyle = line.color;
                    ctx.beginPath();
                    ctx.moveTo(px, 0);
                    ctx.lineTo(px, scope.bitmapSize.height);
                    ctx.stroke();
                }
                ctx.restore();
            });
        },
    };
    const paneView = { renderer: () => renderer };

    return {
        attached(param) {
            chart = param.chart;
            requestUpdate = param.requestUpdate;
        },
        updateAllViews() { },
        paneViews: () => [paneView],
        setLines(newLines) {
            lines = newLines;
            if (requestUpdate) requestUpdate();
        },
    };
}

// Single dashed vertical line at a given time, independent of the multi-line
// createVerticalLinesPrimitive above (that one is a caller-supplied *list* of drawn
// annotations; this is SimulationPage's own single "window start" marker, moved via
// setTime rather than a whole-list replace) -- same technique ohlcvChart.js used for
// this exact marker before this component absorbed its live-trading overlay.
function createWindowLinePrimitive(color) {
    let time = null;
    let x = null;
    let chart = null;
    let requestUpdate = null;

    const renderer = {
        draw(target) {
            if (x === null) return;
            target.useBitmapCoordinateSpace(scope => {
                const ctx = scope.context;
                const px = Math.round(x * scope.horizontalPixelRatio) + 0.5;
                ctx.save();
                ctx.strokeStyle = color;
                ctx.lineWidth = 1;
                ctx.setLineDash([4, 4]);
                ctx.beginPath();
                ctx.moveTo(px, 0);
                ctx.lineTo(px, scope.bitmapSize.height);
                ctx.stroke();
                ctx.restore();
            });
        },
    };
    const paneView = { renderer: () => renderer };

    return {
        attached(param) {
            chart = param.chart;
            requestUpdate = param.requestUpdate;
        },
        updateAllViews() {
            x = time !== null && chart ? chart.timeScale().timeToCoordinate(time) : null;
        },
        paneViews: () => [paneView],
        setTime(t) {
            time = t;
            if (requestUpdate) requestUpdate();
        },
    };
}

// Hand-rolled Series Primitive drawing a soft, translucent vertical band per T-Range/
// T-Range Chart drawing (see ProfilerPage's AllRangeFills), with its "#Id" label near
// the top -- drawBackground (rather than draw) is the Series Primitives API's own hook
// for content that should render behind the candles/volume instead of on top of them,
// which is exactly what "fill background... to see range" needs (a highlight, not an
// overlay that obscures the bars inside it).
function createRangeFillsPrimitive() {
    let fills = []; // { time1, time2, color, label }
    let chart = null;
    let requestUpdate = null;

    const renderer = {
        draw() { },
        drawBackground(target) {
            if (fills.length === 0 || !chart) return;
            target.useMediaCoordinateSpace(scope => {
                const ctx = scope.context;
                for (const fill of fills) {
                    const x1 = chart.timeScale().timeToCoordinate(fill.time1);
                    const x2 = chart.timeScale().timeToCoordinate(fill.time2);
                    if (x1 === null || x2 === null) continue;
                    const left = Math.min(x1, x2);
                    const width = Math.max(1, Math.abs(x2 - x1));
                    ctx.save();
                    ctx.globalAlpha = 0.12;
                    ctx.fillStyle = fill.color;
                    ctx.fillRect(left, 0, width, scope.mediaSize.height);
                    ctx.restore();

                    // Dashed edge lines at time1/time2 -- same dash convention as
                    // createVerticalLinesPrimitive's own lines -- marks the band's
                    // boundaries (for the volume-range fill, the same edges
                    // attachRangeFillDragInteractions' resize-left/resize-right grab
                    // zones are centered on) more clearly than the translucent fill alone.
                    ctx.save();
                    ctx.globalAlpha = 0.7;
                    ctx.strokeStyle = fill.color;
                    ctx.lineWidth = 1;
                    ctx.setLineDash([4, 4]);
                    ctx.beginPath();
                    ctx.moveTo(x1 + 0.5, 0);
                    ctx.lineTo(x1 + 0.5, scope.mediaSize.height);
                    ctx.moveTo(x2 + 0.5, 0);
                    ctx.lineTo(x2 + 0.5, scope.mediaSize.height);
                    ctx.stroke();
                    ctx.restore();

                    if (fill.label) {
                        ctx.save();
                        ctx.font = 'bold 10px monospace';
                        ctx.fillStyle = fill.color;
                        ctx.textAlign = 'left';
                        ctx.textBaseline = 'top';
                        ctx.fillText(fill.label, left + 4, 4);
                        ctx.restore();
                    }
                }
            });
        },
    };
    const paneView = { renderer: () => renderer };

    return {
        attached(param) {
            chart = param.chart;
            requestUpdate = param.requestUpdate;
        },
        updateAllViews() { },
        paneViews: () => [paneView],
        setFills(newFills) {
            fills = newFills;
            if (requestUpdate) requestUpdate();
        },
    };
}

// Volume Range band drag-to-reposition/resize -- lets the user grab the placed band
// (the one RangeFill with colorGroup 'volume', see setLines below; ProfilerPage's
// VolumeRangeFill is its only producer, so that colorGroup is
// exclusive to it), either sliding the whole thing left/right or dragging just one
// edge to shrink/extend it, instead of clearing and re-placing both clicks. Reuses
// OnVolumeRangeClickedAsync (the same callback the initial two clicks dispatch
// through) on drop, since "the range is now these two times" is identical whether it
// came from placing, moving, or resizing.
function volumeRangeFillBounds(entry) {
    if (!entry.volumeRangeFill) return null;
    const ts = entry.chart.timeScale();
    const x1 = ts.timeToCoordinate(entry.volumeRangeFill.time1);
    const x2 = ts.timeToCoordinate(entry.volumeRangeFill.time2);
    if (x1 === null || x2 === null) return null;
    return { left: Math.min(x1, x2), right: Math.max(x1, x2) };
}

// How close (in px) a press has to land to the band's left/right edge to grab that
// edge for resizing rather than the body for moving -- same idea as the minimap's own
// minimapEdgeZonePx.
const rangeFillEdgeZonePx = 6;

// Minimum on-screen gap (in px) kept between a resized edge and the anchored other
// edge, so dragging an edge past its opposite one can't collapse or invert the band.
const rangeFillMinGapPx = 4;

function rangeFillHitTest(bounds, xCss) {
    if (!bounds) return null;
    if (Math.abs(xCss - bounds.left) <= rangeFillEdgeZonePx) return 'resize-left';
    if (Math.abs(xCss - bounds.right) <= rangeFillEdgeZonePx) return 'resize-right';
    if (xCss > bounds.left && xCss < bounds.right) return 'move';
    return null;
}

function attachRangeFillDragInteractions(entry, container) {
    let drag = null;

    // rAF-throttled live commit while dragging -- pointermove can fire far faster than
    // Blazor Server's SignalR round trip (each OnVolumeRangeClickedAsync call re-filters
    // the loaded bars and re-renders both profile charts), so onMove only records the
    // latest frame and this ticks it to .NET at most once per animation frame instead of
    // once per event. onUp cancels any pending frame and always sends one final commit
    // itself, so the drop position is never lost to a frame that got superseded.
    let rafHandle = null;
    let latestResult = null;

    function scheduleLiveCommit(result) {
        latestResult = result;
        if (rafHandle !== null) return;
        rafHandle = requestAnimationFrame(() => {
            rafHandle = null;
            if (latestResult && entry.dotNetRef) {
                entry.dotNetRef.invokeMethodAsync('OnVolumeRangeClickedAsync', latestResult.t1, latestResult.t2);
            }
        });
    }

    function cancelLiveCommit() {
        if (rafHandle !== null) {
            cancelAnimationFrame(rafHandle);
            rafHandle = null;
        }
        latestResult = null;
    }

    // Redraws the band at its dragged/resized position (preview only -- the
    // authoritative change is committed via OnVolumeRangeClickedAsync once the drag
    // ends, which round-trips back down through setLines with the real new fill).
    function previewFill(t1, t2) {
        entry.rangeFills.setFills(entry.rangeFillsRaw.map(f => (f.colorGroup === 'volume'
            ? { time1: t1, time2: t2, label: f.label, color: colorFor(entry, f.colorGroup) }
            : { time1: f.time1, time2: f.time2, label: f.label, color: colorFor(entry, f.colorGroup) })));
    }

    // Returns the new {t1, t2} pair for the current drag at this pointer offset, or
    // null if the cursor has been dragged past where the time scale can resolve a
    // time (e.g. off the loaded edge) -- callers just leave the last valid frame up.
    function renderDrag(deltaPx) {
        const ts = entry.chart.timeScale();

        if (drag.mode === 'move') {
            const x1 = ts.timeToCoordinate(drag.time1);
            const x2 = ts.timeToCoordinate(drag.time2);
            if (x1 === null || x2 === null) return null;
            const t1 = ts.coordinateToTime(x1 + deltaPx);
            const t2 = ts.coordinateToTime(x2 + deltaPx);
            if (t1 === null || t2 === null) return null;
            previewFill(t1, t2);
            return { t1, t2 };
        }

        // resize-left / resize-right: only the grabbed edge moves, clamped so it can't
        // cross the anchored other edge.
        const anchorCoord = ts.timeToCoordinate(drag.anchorTime);
        const movingCoord0 = ts.timeToCoordinate(drag.movingTime);
        if (anchorCoord === null || movingCoord0 === null) return null;
        let movingCoord = movingCoord0 + deltaPx;
        movingCoord = drag.mode === 'resize-left'
            ? Math.min(movingCoord, anchorCoord - rangeFillMinGapPx)
            : Math.max(movingCoord, anchorCoord + rangeFillMinGapPx);
        const movingTime = ts.coordinateToTime(movingCoord);
        if (movingTime === null) return null;

        const t1 = drag.movingIsTime1 ? movingTime : drag.anchorTime;
        const t2 = drag.movingIsTime1 ? drag.anchorTime : movingTime;
        previewFill(t1, t2);
        return { t1, t2 };
    }

    // Capture phase + stopPropagation so a press that lands on the band is claimed here
    // before lightweight-charts' own canvas handlers see it and start a pan/crosshair
    // drag instead. Only claims the event when a volume-range band exists, no draw tool
    // is currently armed (placing a new line takes priority), and the press actually
    // landed within (or on the edge of) the band's horizontal bounds.
    container.addEventListener('pointerdown', e => {
        if (entry.drawMode || !entry.volumeRangeFill) return;
        const bounds = volumeRangeFillBounds(entry);
        const rect = container.getBoundingClientRect();
        const xCss = e.clientX - rect.left;
        const hit = rangeFillHitTest(bounds, xCss);
        if (!hit) return;

        e.stopPropagation();
        e.preventDefault();

        if (hit === 'move') {
            drag = { mode: 'move', time1: entry.volumeRangeFill.time1, time2: entry.volumeRangeFill.time2, startClientX: e.clientX };
        } else {
            const ts = entry.chart.timeScale();
            const time1IsLeft = ts.timeToCoordinate(entry.volumeRangeFill.time1) <= ts.timeToCoordinate(entry.volumeRangeFill.time2);
            const movingIsTime1 = (hit === 'resize-left') === time1IsLeft;
            drag = {
                mode: hit,
                movingIsTime1,
                movingTime: movingIsTime1 ? entry.volumeRangeFill.time1 : entry.volumeRangeFill.time2,
                anchorTime: movingIsTime1 ? entry.volumeRangeFill.time2 : entry.volumeRangeFill.time1,
                startClientX: e.clientX,
            };
        }
        entry.chart.applyOptions({ handleScroll: false, handleScale: false });
        container.style.cursor = hit === 'move' ? 'grabbing' : 'ew-resize';

        const onMove = moveEvt => {
            const result = renderDrag(moveEvt.clientX - drag.startClientX);
            if (result) scheduleLiveCommit(result);
        };

        const onUp = upEvt => {
            window.removeEventListener('pointermove', onMove);
            window.removeEventListener('pointerup', onUp);
            entry.chart.applyOptions({ handleScroll: true, handleScale: true });
            container.style.cursor = '';

            cancelLiveCommit();
            const result = renderDrag(upEvt.clientX - drag.startClientX);
            drag = null;
            if (result && entry.dotNetRef) {
                entry.dotNetRef.invokeMethodAsync('OnVolumeRangeClickedAsync', result.t1, result.t2);
            }
        };

        window.addEventListener('pointermove', onMove);
        window.addEventListener('pointerup', onUp);
    }, { capture: true });

    // Hover-only cursor feedback (no active drag) so the band reads as grabbable/
    // resizable -- reset back to the default cursor once a draw tool arms or the band
    // is cleared, rather than leaving a stale grab/resize cursor showing.
    container.addEventListener('pointermove', e => {
        if (drag) return;
        if (entry.drawMode || !entry.volumeRangeFill) { container.style.cursor = ''; return; }
        const bounds = volumeRangeFillBounds(entry);
        const rect = container.getBoundingClientRect();
        const xCss = e.clientX - rect.left;
        const hit = rangeFillHitTest(bounds, xCss);
        container.style.cursor = hit === 'move' ? 'grab' : hit ? 'ew-resize' : '';
    });
}

// How many chart clicks each draw mode needs before it's complete and dispatched to
// .NET (see the subscribeClick handler in create() below) -- T-Range/P-Range/Rectangle/
// T-Range-Chart place plain horizontal/vertical lines under the hood (2, 2, 2+2, and 2
// respectively), not a shape of their own, so they need no entry in
// createDiagonalLinesPrimitive or setLines below, unlike 'diagonal'. T-Range Reference
// Chart is T-Range Chart's 2 clicks plus one more for its reference line.
const modeClickCounts = {
    horizontal: 1, vertical: 1, diagonal: 2, trange: 2, prange: 2, rectangle: 4,
    trangechart: 2, trangereferencechart: 3, volumerange: 2,
};

// "What to click next" text for the toolbar hint (see OnDrawHintAsync) -- kept here
// rather than mirrored in C# since only this module tracks how many clicks have
// landed so far (entry.drawClicks), including Diagonal's first click, which produces
// no line/callback on the .NET side a progress message could otherwise be derived
// from. clickIndex is 0-based and always refers to the *next* click still needed
// (so 0 means "about to place the first one").
function drawHint(mode, clickIndex) {
    switch (mode) {
        case 'horizontal': return 'click a point to place the price line';
        case 'vertical': return 'click a point to place the time line';
        case 'diagonal':
            return clickIndex === 0 ? 'click the start point (1 of 2)' : 'click the end point (2 of 2)';
        case 'trange': return `click a time point (${clickIndex + 1} of 2)`;
        case 'prange': return `click a price point (${clickIndex + 1} of 2)`;
        case 'rectangle': return clickIndex < 2
            ? `click a time bound (${clickIndex + 1} of 2)`
            : `click a price bound (${clickIndex - 1} of 2)`;
        case 'trangechart': return `click a time point (${clickIndex + 1} of 2) for the chart range`;
        case 'trangereferencechart': return clickIndex < 2
            ? `click a time point (${clickIndex + 1} of 2) for the chart range`
            : 'click a point before the earlier line to place the reference window';
        case 'volumerange': return `click a time point (${clickIndex + 1} of 2) for the volume profile range`;
        default: return 'click the chart to place it';
    }
}

// Hand-rolled Series Primitive drawing an arbitrary set of two-point trend lines, each
// in its own colour -- same technique as createVerticalLinesPrimitive above, except
// each line needs both a price and a time coordinate, so this one captures `series`
// (for priceToCoordinate) as well as `chart` from attached().
function createDiagonalLinesPrimitive() {
    let lines = []; // { time1, price1, time2, price2, color }
    let chart = null;
    let series = null;
    let requestUpdate = null;

    const renderer = {
        draw(target) {
            if (lines.length === 0 || !chart || !series) return;
            target.useBitmapCoordinateSpace(scope => {
                const ctx = scope.context;
                ctx.save();
                ctx.lineWidth = 1.5;
                for (const line of lines) {
                    ctx.strokeStyle = line.color;
                    const x1 = chart.timeScale().timeToCoordinate(line.time1);
                    const y1 = series.priceToCoordinate(line.price1);
                    const x2 = chart.timeScale().timeToCoordinate(line.time2);
                    const y2 = series.priceToCoordinate(line.price2);
                    if (x1 === null || y1 === null || x2 === null || y2 === null) continue;
                    ctx.beginPath();
                    ctx.moveTo(x1 * scope.horizontalPixelRatio, y1 * scope.verticalPixelRatio);
                    ctx.lineTo(x2 * scope.horizontalPixelRatio, y2 * scope.verticalPixelRatio);
                    ctx.stroke();
                }
                ctx.restore();
            });
        },
    };
    const paneView = { renderer: () => renderer };

    return {
        attached(param) {
            chart = param.chart;
            series = param.series;
            requestUpdate = param.requestUpdate;
        },
        updateAllViews() { },
        paneViews: () => [paneView],
        setLines(newLines) {
            lines = newLines;
            if (requestUpdate) requestUpdate();
        },
    };
}

// ── Visible-data window (minimap) ───────────────────────────────────────────
// A thin sparkline strip below the chart tracing every loaded bar's close price,
// with a translucent box over the portion currently visible on the main chart --
// lightweight-charts has no built-in minimap/range-slider (unlike TradingView's
// paid charting library), so this is hand-rolled the same way the drawing
// primitives above are. Logical index space (0..bars.length-1) is shared 1:1 with
// chart.timeScale()'s own logical range, so no time<->index lookup is needed --
// bar i's x position and a visible range's from/to all map through the same
// logicalToPx/pxToLogicalDelta pair.

// Fixed regardless of data size -- small enough not to eat into the chart's own
// space, tall enough for the sparkline shape and window box to read clearly.
const minimapEdgeZonePx = 6;

// Highest logical index the minimap's track represents -- always the last loaded
// bar, never the live (possibly zoomed-out-past-data) visible range, so the
// sparkline's full width always matches the full loaded dataset (the same
// convention as Highcharts' Navigator/dygraphs' rangeSelector).
function minimapSpan(entry) {
    const totalBars = entry.bars ? entry.bars.length : 0;
    return Math.max(1, totalBars - 1);
}

function minimapWidthPx(entry) {
    return entry.minimap.canvas.getBoundingClientRect().width || 1;
}

function logicalToPx(entry, logical) {
    return (logical / minimapSpan(entry)) * minimapWidthPx(entry);
}

function pxToLogicalDelta(entry, deltaPx) {
    return (deltaPx / minimapWidthPx(entry)) * minimapSpan(entry);
}

function clampLogical(entry, logical) {
    return Math.max(0, Math.min(minimapSpan(entry), logical));
}

// Resizes the backing canvas to the container's current box (device-pixel-ratio
// aware, same reasoning as lightweight-charts' own bitmap coordinate space) --
// called on creation and on every ResizeObserver tick, since .doc-minimap's width
// changes whenever the chart expands/collapses or the window resizes.
function resizeMinimapCanvas(entry) {
    const canvas = entry.minimap.canvas;
    const rect = entry.minimap.container.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));
    drawMinimap(entry);
}

// Redraws the full sparkline + visible-window overlay. Cheap enough (a couple
// hundred points at most, per ProfilerPage's BarsPerFetch) to just redraw
// wholesale on every visible-range change rather than diffing.
function drawMinimap(entry) {
    const mm = entry.minimap;
    if (!mm) return;
    const canvas = mm.canvas;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, rect.width, rect.height);

    const bars = entry.bars || [];
    if (bars.length === 0 || rect.width === 0 || rect.height === 0) return;

    let min = Infinity, max = -Infinity;
    for (const b of bars) {
        if (b.close < min) min = b.close;
        if (b.close > max) max = b.close;
    }
    if (min === max) { min -= 1; max += 1; }

    const padY = rect.height * 0.12;
    const plotH = rect.height - padY * 2;
    const yForClose = c => padY + plotH - ((c - min) / (max - min)) * plotH;

    const tracePath = new Path2D();
    bars.forEach((b, i) => {
        const x = logicalToPx(entry, i);
        const y = yForClose(b.close);
        if (i === 0) tracePath.moveTo(x, y); else tracePath.lineTo(x, y);
    });

    const areaPath = new Path2D(tracePath);
    areaPath.lineTo(rect.width, rect.height);
    areaPath.lineTo(0, rect.height);
    areaPath.closePath();

    ctx.save();
    ctx.globalAlpha = 0.1;
    ctx.fillStyle = entry.accentColor;
    ctx.fill(areaPath);
    ctx.restore();

    ctx.save();
    ctx.globalAlpha = 0.55;
    ctx.strokeStyle = entry.accentColor;
    ctx.lineWidth = 1.25;
    ctx.stroke(tracePath);
    ctx.restore();

    const range = entry.chart.timeScale().getVisibleLogicalRange();
    if (!range) return;

    const left = logicalToPx(entry, clampLogical(entry, range.from));
    const right = logicalToPx(entry, clampLogical(entry, range.to));

    ctx.save();
    ctx.globalAlpha = 0.18;
    ctx.fillStyle = entry.accentColor;
    ctx.fillRect(left, 0, Math.max(1, right - left), rect.height);
    ctx.restore();

    ctx.save();
    ctx.strokeStyle = entry.accentColor;
    ctx.lineWidth = 1;
    ctx.strokeRect(left + 0.5, 0.5, Math.max(1, right - left - 1), rect.height - 1);
    // Slightly thicker edge grips hint that the box's sides (not just its body)
    // are draggable, for zooming rather than panning.
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(left, 0); ctx.lineTo(left, rect.height);
    ctx.moveTo(right, 0); ctx.lineTo(right, rect.height);
    ctx.stroke();
    ctx.restore();
}

// Applies a new visible logical range after clamping it to stay within (or just
// barely past, for pan) the loaded data -- shared by drag-to-pan, edge-drag-to-
// zoom, and click-to-jump below. Redraws immediately rather than waiting on
// subscribeVisibleLogicalRangeChange so dragging feels responsive even if that
// event is delivered a frame late.
function applyMinimapRange(entry, from, to) {
    const totalBars = entry.bars ? entry.bars.length : 0;
    if (totalBars === 0) return;

    const minWidth = Math.min(3, minimapSpan(entry));
    if (to - from < minWidth) {
        const mid = (from + to) / 2;
        from = mid - minWidth / 2;
        to = mid + minWidth / 2;
    }

    entry.chart.timeScale().setVisibleLogicalRange({ from, to });
    drawMinimap(entry);
}

// left/right: pixel bounds of the current visible-window box, for hit-testing
// clicks against it (edges vs. body vs. outside).
function minimapWindowBounds(entry) {
    const range = entry.chart.timeScale().getVisibleLogicalRange();
    if (!range) return null;
    return {
        from: range.from,
        to: range.to,
        left: logicalToPx(entry, clampLogical(entry, range.from)),
        right: logicalToPx(entry, clampLogical(entry, range.to)),
    };
}

function minimapHitTest(entry, xCss) {
    const bounds = minimapWindowBounds(entry);
    if (!bounds) return null;
    if (Math.abs(xCss - bounds.left) <= minimapEdgeZonePx) return 'resize-left';
    if (Math.abs(xCss - bounds.right) <= minimapEdgeZonePx) return 'resize-right';
    if (xCss > bounds.left && xCss < bounds.right) return 'pan';
    return 'jump';
}

function attachMinimapInteractions(entry) {
    const canvas = entry.minimap.canvas;

    canvas.addEventListener('pointermove', e => {
        if (entry.minimap.dragMode) {
            const rect = canvas.getBoundingClientRect();
            const deltaLogical = pxToLogicalDelta(entry, (e.clientX - rect.left) - entry.minimap.dragStartX);
            const start = entry.minimap.dragStartRange;
            if (!start) return;

            if (entry.minimap.dragMode === 'pan') {
                const width = start.to - start.from;
                applyMinimapRange(entry, start.from + deltaLogical, start.from + deltaLogical + width);
            } else if (entry.minimap.dragMode === 'resize-left') {
                applyMinimapRange(entry, start.from + deltaLogical, start.to);
            } else if (entry.minimap.dragMode === 'resize-right') {
                applyMinimapRange(entry, start.from, start.to + deltaLogical);
            }
            return;
        }

        if (!entry.bars || entry.bars.length === 0) { canvas.style.cursor = 'default'; return; }
        const rect = canvas.getBoundingClientRect();
        const hit = minimapHitTest(entry, e.clientX - rect.left);
        canvas.style.cursor = hit === 'resize-left' || hit === 'resize-right' ? 'ew-resize'
            : hit === 'pan' ? 'grab' : 'pointer';
    });

    canvas.addEventListener('pointerdown', e => {
        if (!entry.bars || entry.bars.length === 0) return;
        const rect = canvas.getBoundingClientRect();
        const xCss = e.clientX - rect.left;
        const hit = minimapHitTest(entry, xCss);
        if (!hit) return;

        const range = entry.chart.timeScale().getVisibleLogicalRange();
        if (!range) return;

        if (hit === 'jump') {
            // Clicking the bare track centers the current window on the clicked
            // point immediately, then lets the same drag continue as a pan --
            // standard scrollbar-track behaviour.
            const width = range.to - range.from;
            const clicked = (xCss / minimapWidthPx(entry)) * minimapSpan(entry);
            applyMinimapRange(entry, clicked - width / 2, clicked + width / 2);
            entry.minimap.dragMode = 'pan';
            entry.minimap.dragStartRange = entry.chart.timeScale().getVisibleLogicalRange();
        } else {
            entry.minimap.dragMode = hit;
            entry.minimap.dragStartRange = range;
        }

        entry.minimap.dragStartX = xCss;
        canvas.setPointerCapture(e.pointerId);
        canvas.style.cursor = entry.minimap.dragMode === 'pan' ? 'grabbing' : 'ew-resize';
    });

    function endDrag(e) {
        if (!entry.minimap.dragMode) return;
        entry.minimap.dragMode = null;
        entry.minimap.dragStartRange = null;
        try { canvas.releasePointerCapture(e.pointerId); } catch { /* already released */ }
        canvas.style.cursor = 'default';
    }
    canvas.addEventListener('pointerup', endDrag);
    canvas.addEventListener('pointercancel', endDrag);
}

// minimapContainer: MarketChart.razor's .doc-minimap div -- the canvas
// itself is created here (not in .razor markup) since it needs no Blazor-side
// state, same reasoning as the tooltip element below.
function createMinimap(minimapContainer, interactive) {
    const canvas = document.createElement('canvas');
    // pointer-events:none as a belt-and-suspenders alongside skipping
    // attachMinimapInteractions below -- non-interactive callers get a purely visual
    // sparkline with no drag-to-pan/resize-to-zoom surface at all.
    canvas.style.cssText = interactive === false
        ? 'display:block;width:100%;height:100%;pointer-events:none;'
        : 'display:block;width:100%;height:100%;';
    minimapContainer.appendChild(canvas);

    const minimap = { canvas, container: minimapContainer, dragMode: null, dragStartX: 0, dragStartRange: null };
    return minimap;
}

export function create(container, minimapContainer, dotNetRef, interactive) {
    const chart = LightweightCharts.createChart(container, {
        layout: {
            background: { type: 'solid', color: 'transparent' }, textColor: '#c9c9c9',
            panes: { enableResize: true, separatorColor: 'rgba(255,255,255,0.12)', separatorHoverColor: 'rgba(255,255,255,0.3)' },
        },
        grid: {
            vertLines: { color: 'rgba(255,255,255,0.06)' },
            horzLines: { color: 'rgba(255,255,255,0.06)' },
        },
        rightPriceScale: { borderColor: 'rgba(255,255,255,0.15)' },
        timeScale: { borderColor: 'rgba(255,255,255,0.15)', timeVisible: true, secondsVisible: false, rightOffset: 10 },
        // Non-interactive callers (EpisodePlayback's live episode chart) get a fixed, read-
        // only view -- no scroll/pinch/drag pan-zoom and no crosshair, since letting the
        // user drag the view around while Bars keeps changing underneath them is what
        // crashes lightweight-charts internally (see MarketChart.razor.cs's
        // Interactive parameter doc comment).
        handleScroll: interactive !== false,
        handleScale: interactive !== false,
        crosshair: { mode: interactive === false ? LightweightCharts.CrosshairMode.Hidden : LightweightCharts.CrosshairMode.Normal },
        // Reacts to the container's own size (via ResizeObserver) so the expand/collapse
        // toggle in MarketChart.razor.cs needs no explicit resize call from .NET.
        autoSize: true,
    });

    // handleScroll/handleScale/CrosshairMode.Hidden above stop lightweight-charts from
    // acting on mouse input, but the library still tracks hover position internally for
    // its own hit-testing (crosshair/marker layout) and that bookkeeping is what crashed
    // mid setData() -- the mouse was hovering a point that no longer resolved once Bars
    // changed underneath it. pointer-events:none stops the DOM from ever delivering a
    // mouse/touch event to the chart at all, so none of that internal state exists.
    if (interactive === false) container.style.pointerEvents = 'none';

    const candleSeries = chart.addSeries(LightweightCharts.CandlestickSeries, {
        upColor, downColor, borderVisible: false, wickUpColor: upColor, wickDownColor: downColor,
        priceLineVisible: false, lastValueVisible: false,
    });

    const volumeSeries = chart.addSeries(LightweightCharts.HistogramSeries, {
        priceFormat: { type: 'volume' },
    }, 1);
    chart.panes()[0].setStretchFactor(3);
    chart.panes()[1].setStretchFactor(1);

    // VWAP overlay -- a plain native LineSeries rather than one of the hand-rolled
    // primitives below, since lightweight-charts already renders a time-indexed price
    // series on its own; ProfilerPage recomputes the points from its own loaded bars
    // (see MarketChart.razor.cs's VwapLine parameter) and this just displays them.
    const vwapColor = getComputedStyle(container).getPropertyValue('--color-tertiary').trim() || '#bd00ff';
    const vwapSeries = chart.addSeries(LightweightCharts.LineSeries, {
        color: vwapColor, lineWidth: 2, priceLineVisible: false, lastValueVisible: true,
        crosshairMarkerVisible: false, title: 'VWAP',
    });

    const tooltip = document.createElement('div');
    tooltip.style.cssText =
        'display:none;position:absolute;pointer-events:none;z-index:5;' +
        'background:rgba(10,10,10,0.9);border:1px solid rgba(255,255,255,0.15);border-radius:4px;' +
        'padding:6px 8px;font-size:0.75rem;font-family:monospace;color:#e6e6e6;white-space:nowrap;';
    container.style.position = 'relative';
    container.appendChild(tooltip);

    chart.subscribeCrosshairMove(param => {
        if (interactive === false) return;
        const bar = param.time ? param.seriesData.get(candleSeries) : undefined;
        if (!param.point || param.point.x < 0 || param.point.y < 0 || !bar) {
            tooltip.style.display = 'none';
            return;
        }

        const vol = param.seriesData.get(volumeSeries);
        const vwap = param.seriesData.get(vwapSeries);
        const d = new Date(param.time * 1000);
        tooltip.innerHTML =
            `<div>${d.toISOString().slice(0, 16).replace('T', ' ')}</div>` +
            `<div>O ${bar.open.toFixed(2)}  H ${bar.high.toFixed(2)}  L ${bar.low.toFixed(2)}  C ${bar.close.toFixed(2)}</div>` +
            (vol ? `<div>Vol ${vol.value.toLocaleString()}</div>` : '') +
            (vwap ? `<div>VWAP ${vwap.value.toFixed(5)}</div>` : '');

        tooltip.style.display = 'block';
        const left = Math.min(param.point.x + 12, container.clientWidth - tooltip.offsetWidth - 8);
        tooltip.style.left = `${Math.max(left, 8)}px`;
        tooltip.style.top = `${Math.max(param.point.y - 12, 8)}px`;
    });

    // Same custom properties MarketChartToolbar.razor.css's .doc-tool-btn-* group
    // modifiers use (base.css defines each --color-* per theme) -- read once here so
    // drawn shapes are colour-coded by the same Lines/Ranges/Shapes/Charts
    // classification as the toolbar buttons that place them (see setLines' colorFor).
    const style = getComputedStyle(container);
    const accentColor = style.getPropertyValue('--color-accent').trim() || '#ff6b9d';
    const groupColors = {
        lines: style.getPropertyValue('--color-primary').trim() || accentColor,
        ranges: style.getPropertyValue('--color-secondary').trim() || accentColor,
        shapes: style.getPropertyValue('--color-warning').trim() || accentColor,
        charts: accentColor,
        // T-Range Reference Chart's third ("reference window") line/fill -- deliberately
        // its own colour, distinct from charts' accentColor, per "should also have some
        // colour but different".
        reference: style.getPropertyValue('--color-error').trim() || accentColor,
        // Volume Range's own fill -- see MarketChartToolbar.razor.css's .doc-tool-btn-profile.
        volume: style.getPropertyValue('--color-info').trim() || accentColor,
        // POC/VAH/VAL price lines, computed (not user-drawn) -- see ProfilerPage's
        // ProfileLevelLines. Its own colour, distinct from every drawn-tool group above.
        profile: style.getPropertyValue('--color-highlight').trim() || accentColor,
        // Average/Median price lines, computed (not user-drawn) -- see ProfilerPage's
        // AverageMedianLines. --color-tertiary is already vwapColor's line above, so
        // average pulls from the --color-badge-* set instead (a wider, already-themed
        // palette meant for exactly this "another mutually-distinct swatch" need).
        // median's --color-neutral is otherwise unclaimed by any chart element.
        average: style.getPropertyValue('--color-badge-2').trim() || accentColor,
        median: style.getPropertyValue('--color-neutral').trim() || accentColor,
        // Closed-trade diagonal lines (entry -> exit), computed not user-drawn -- see
        // EpisodePlayback's episode replay, which colours each trade by its realized P&L
        // sign rather than by draw tool.
        win: style.getPropertyValue('--color-success').trim() || accentColor,
        loss: style.getPropertyValue('--color-error').trim() || accentColor,
    };

    // Trade-action markers (see MarketChart.razor.cs's Markers parameter) --
    // lightweight-charts' own native series-markers plugin (v5+) rather than a
    // hand-rolled primitive like the lines above, since arrows/circles pinned to a
    // bar's own OHLC position are exactly what it's built for.
    const actionMarkers = LightweightCharts.createSeriesMarkers(candleSeries, []);

    const verticalLines = createVerticalLinesPrimitive();
    candleSeries.attachPrimitive(verticalLines);
    const diagonalLines = createDiagonalLinesPrimitive();
    candleSeries.attachPrimitive(diagonalLines);
    const rangeFills = createRangeFillsPrimitive();
    candleSeries.attachPrimitive(rangeFills);

    // SimulationPage's window-start marker, plus the SL/TP colours its position-level
    // lines use -- same theme-aware --color-error/--color-success pair convention as
    // groupColors.reference/win/loss above.
    const windowLine = createWindowLinePrimitive(accentColor);
    candleSeries.attachPrimitive(windowLine);
    const slColor = style.getPropertyValue('--color-error').trim() || downColor;
    const tpColor = style.getPropertyValue('--color-success').trim() || upColor;

    // Volume Range's own first-click preview -- same createWindowLinePrimitive single-
    // line primitive as windowLine above, shown the instant the first of Volume Range's
    // two clicks lands (see subscribeClick's 'volumerange' case) so it reads the same as
    // vertical/T-Range's own first click, which already renders immediately via
    // OnVerticalLineClickedAsync. Kept purely client-side (its own primitive, not folded
    // into verticalLines) since Volume Range's clicks deliberately never dispatch
    // OnVerticalLineDrawn -- see OnVolumeRangeDrawn's doc comment on the .NET side.
    const volumeRangePreviewLine = createWindowLinePrimitive(groupColors.volume);
    candleSeries.attachPrimitive(volumeRangePreviewLine);

    const id = crypto.randomUUID();
    const entry = {
        chart, candleSeries, volumeSeries, vwapSeries, tooltip, verticalLines, diagonalLines, rangeFills, actionMarkers, accentColor, groupColors,
        horizontalLineObjs: [], dotNetRef, hasData: false, pendingLoadMore: false,
        drawMode: null, drawClicks: [], bars: [], minimap: null,
        windowLine, slColor, tpColor, entryColor: accentColor,
        bidLine: null, askLine: null, slLine: null, tpLine: null, entryLine: null,
        rangeFillsRaw: [], volumeRangeFill: null, volumeRangePreviewLine,
    };
    instances.set(id, entry);

    if (interactive !== false) attachRangeFillDragInteractions(entry, container);

    entry.minimap = createMinimap(minimapContainer, interactive);
    if (interactive !== false) attachMinimapInteractions(entry);
    const minimapResizeObserver = new ResizeObserver(() => resizeMinimapCanvas(entry));
    minimapResizeObserver.observe(minimapContainer);
    entry.minimap.resizeObserver = minimapResizeObserver;
    resizeMinimapCanvas(entry);

    // Infinite-history backfill: once the visible range gets within
    // nearStartThreshold bars of the oldest loaded bar, ask .NET (ProfilerPage, via
    // MarketChart.OnNearStartAsync) for the next older chunk. pendingLoadMore
    // guards against re-firing on every pan-tick while that round trip is in flight
    // or if the caller has no more data to give -- cleared by resumeLoadMore() once
    // the round trip finishes either way. Also keeps the minimap's window box in
    // sync with every pan/zoom the user does directly on the main chart.
    chart.timeScale().subscribeVisibleLogicalRangeChange(range => {
        drawMinimap(entry);
        if (!range || !entry.hasData || entry.pendingLoadMore || !entry.dotNetRef) return;
        if (range.from <= nearStartThreshold) {
            entry.pendingLoadMore = true;
            entry.dotNetRef.invokeMethodAsync('OnNearStartAsync').catch(() => { entry.pendingLoadMore = false; });
        }
    });

    // Click-to-draw: only acts while a mode is armed (see setDrawMode), so normal
    // pan/zoom/crosshair use of the chart is unaffected otherwise. Each click that
    // already forms a complete line on its own (every mode except 'diagonal', where a
    // single point isn't a line yet) dispatches immediately rather than waiting for the
    // rest of the sequence -- so T-Range/P-Range/Rectangle draw one line per click as
    // you place them instead of all-at-once at the end. Once the armed mode's full
    // click count is reached (modeClickCounts), OnDrawFinishedAsync disarms it -- mirrored
    // on the .NET side so the toolbar's active button resets in step with it.
    chart.subscribeClick(param => {
        if (!entry.drawMode || !param.point || !entry.dotNetRef) return;
        const price = candleSeries.coordinateToPrice(param.point.y);
        const time = param.time;
        if (price === null || time == null) return;

        const mode = entry.drawMode;
        const ref = entry.dotNetRef;

        // T-Range Reference Chart's 3rd click (the reference line) must land before the
        // earlier of the first two -- reject it (leave the tool armed, drawClicks
        // untouched) rather than counting it, telling the user why via the hint so they
        // know to just try again instead of wondering why nothing happened.
        if (mode === 'trangereferencechart' && entry.drawClicks.length === 2) {
            const earliest = Math.min(entry.drawClicks[0].time, entry.drawClicks[1].time);
            if (time >= earliest) {
                ref.invokeMethodAsync(
                    'OnDrawHintAsync', "that point isn't before the earlier line -- click before it to place the reference window");
                return;
            }
        }

        entry.drawClicks.push({ time, price });
        const clickIndex = entry.drawClicks.length;

        switch (mode) {
            case 'horizontal':
            case 'prange':
                ref.invokeMethodAsync('OnHorizontalLineClickedAsync', price);
                break;
            case 'vertical':
            case 'trange':
            case 'trangechart':
                // T-Range Chart draws the same two vertical lines T-Range does -- the mini
                // chart itself is dispatched separately below, once both clicks are in.
                ref.invokeMethodAsync('OnVerticalLineClickedAsync', time);
                break;
            case 'trangereferencechart':
                // First two clicks are T-Range Chart's own vertical lines; the third is
                // the reference line, dispatched through its own callback so the caller
                // can colour/group it separately (see OnReferenceLineDrawn).
                if (clickIndex <= 2) ref.invokeMethodAsync('OnVerticalLineClickedAsync', time);
                else ref.invokeMethodAsync('OnReferenceLineClickedAsync', time);
                break;
            case 'rectangle':
                // First two clicks are the vertical (time) bounds, last two the
                // horizontal (price) bounds -- see the RECT toolbar button's tooltip.
                if (clickIndex <= 2) ref.invokeMethodAsync('OnVerticalLineClickedAsync', time);
                else ref.invokeMethodAsync('OnHorizontalLineClickedAsync', price);
                break;
            case 'diagonal':
                // A single point isn't a line yet -- nothing to draw until the second
                // click completes it below.
                break;
            case 'volumerange':
                // Never dispatches a generic vertical line -- see OnVolumeRangeDrawn's
                // doc comment on the .NET side. The first click still renders immediately
                // though, same as vertical/trange's own first line, via its own
                // client-side-only preview primitive (cleared once both clicks land,
                // below, or the mode is cancelled/changed -- see setDrawMode).
                if (clickIndex === 1) entry.volumeRangePreviewLine.setTime(time);
                break;
        }

        const needed = modeClickCounts[mode] || 1;
        if (entry.drawClicks.length < needed) {
            ref.invokeMethodAsync('OnDrawHintAsync', drawHint(mode, entry.drawClicks.length));
            return;
        }

        if (mode === 'diagonal') {
            const clicks = entry.drawClicks;
            ref.invokeMethodAsync(
                'OnDiagonalLineClickedAsync', clicks[0].price, clicks[0].time, clicks[1].price, clicks[1].time);
        } else if (mode === 'trangechart' || mode === 'trangereferencechart') {
            const clicks = entry.drawClicks;
            ref.invokeMethodAsync('OnTRangeChartClickedAsync', clicks[0].time, clicks[1].time);
        } else if (mode === 'volumerange') {
            const clicks = entry.drawClicks;
            entry.volumeRangePreviewLine.setTime(null);
            ref.invokeMethodAsync('OnVolumeRangeClickedAsync', clicks[0].time, clicks[1].time);
        }

        entry.drawMode = null;
        entry.drawClicks = [];
        ref.invokeMethodAsync('OnDrawFinishedAsync');
    });

    return id;
}

// resetView: true fits/rezooms to the new data (a fresh load); false preserves the
// current pan/zoom, for a backfill that prepends older bars without the data the
// user is currently looking at (or its position on screen) changing.
export function setData(id, bars, resetView) {
    const entry = instances.get(id);
    if (!entry) return;

    // Captured before entry.bars/hasData are overwritten below -- distinguishes a
    // genuinely fresh dataset from one that's merely grown in place (Episode Playback's
    // replay reveals one more bar per tick, each a resetView=true call with the same
    // underlying session).
    const isFreshLoad = !entry.hasData || bars.length < entry.bars.length;

    entry.candleSeries.setData(bars.map(b => ({ time: b.time, open: b.open, high: b.high, low: b.low, close: b.close })));
    entry.volumeSeries.setData(bars.map(b => ({
        time: b.time, value: b.volume, color: b.close >= b.open ? upColor : downColor,
    })));

    entry.bars = bars;
    entry.hasData = bars.length > 0;
    if (resetView) {
        const ts = entry.chart.timeScale();
        ts.fitContent();
        // leftOffsetBars is a fixed pad, meant for a fresh view's first bar only -- re-
        // adding it on every re-fit of a growing-in-place dataset compounds each tick and,
        // against a still-small bar count, reads as the chart repeatedly zooming out and
        // squeezing its bars to the right.
        if (isFreshLoad) {
            const range = ts.getVisibleLogicalRange();
            if (range) ts.setVisibleLogicalRange({ from: range.from - leftOffsetBars, to: range.to });
        }
    }
    drawMinimap(entry);
}

// points: [{ time, price }] -- see MarketChart.razor.cs's VwapLine parameter.
export function setVwapLine(id, points) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.vwapSeries.setData(points.map(p => ({ time: p.time, value: p.price })));
}

function updatePriceLine(series, line, price, color, title, lineStyle = LightweightCharts.LineStyle.Dashed) {
    if (price == null) {
        if (line) series.removePriceLine(line);
        return null;
    }
    if (line) {
        line.applyOptions({ price });
        return line;
    }
    return series.createPriceLine({
        price, color, title,
        lineWidth: 1,
        lineStyle,
        axisLabelVisible: true,
    });
}

// Live bid/ask as horizontal price lines pinned to the right axis -- updated in place
// via applyOptions rather than remove+recreate, since this fires on every tick (see
// MarketChart.razor.cs's CurrentBid/CurrentAsk parameters).
export function setCurrentPrices(id, bid, ask) {
    const entry = instances.get(id);
    if (!entry) return;

    entry.bidLine = updatePriceLine(entry.candleSeries, entry.bidLine, bid, downColor, 'BID');
    entry.askLine = updatePriceLine(entry.candleSeries, entry.askLine, ask, upColor, 'ASK');
}

// SimulationPage's selected trader's open position: entry price (solid, so it reads as
// "where you are" against SL/TP's dashed "where it'll auto-close") plus SL/TP --
// entryPrice/stopLoss/takeProfit each null independently removes just that line (flat,
// or a position missing that field).
export function setPositionLevels(id, entryPrice, isLong, stopLoss, takeProfit) {
    const entry = instances.get(id);
    if (!entry) return;

    const entryTitle = isLong == null ? 'ENTRY' : (isLong ? 'LONG' : 'SHORT');
    entry.entryLine = updatePriceLine(
        entry.candleSeries, entry.entryLine, entryPrice, entry.entryColor, entryTitle, LightweightCharts.LineStyle.Solid);
    entry.slLine = updatePriceLine(entry.candleSeries, entry.slLine, stopLoss, entry.slColor, 'SL');
    entry.tpLine = updatePriceLine(entry.candleSeries, entry.tpLine, takeProfit, entry.tpColor, 'TP');
}

// Vertical marker for where SimulationPage's tick/bar window (feeding the volume-profile
// panel) starts -- null hides it (e.g. once the whole buffer fits inside the window).
export function setWindowStart(id, unixSeconds) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.windowLine.setTime(unixSeconds ?? null);
}

// mode: one of modeClickCounts' keys, or null (null/falsy disarms drawing entirely).
// Also resets any in-progress multi-click sequence (e.g. Diagonal's first click, or
// T-Range/Rectangle's partial set of points) -- switching tools or re-clicking the
// same toolbar button to cancel shouldn't leave stale clicks for the next placement.
// Sends the freshly-armed tool's first hint (or clears it on disarm) so the toolbar
// hint span never shows stale text left over from whatever was armed before.
export function setDrawMode(id, mode) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.drawMode = mode || null;
    entry.drawClicks = [];
    entry.volumeRangePreviewLine.setTime(null);
    if (entry.dotNetRef) {
        entry.dotNetRef.invokeMethodAsync('OnDrawHintAsync', entry.drawMode ? drawHint(entry.drawMode, 0) : null);
    }
}

// group: one of entry.groupColors' keys ('lines'/'ranges'/'shapes'/'charts', see
// ChartHorizontalLine/VerticalLine/DiagonalLine/RangeFill's ColorGroup), or anything
// else (null, unrecognized) falls back to the chart's plain accent colour.
function colorFor(entry, group) {
    return (group && entry.groupColors[group]) || entry.accentColor;
}

// Replaces the full set of rendered annotation lines -- small counts expected (a
// handful of user-drawn markers), so clear-and-redraw-all is simpler than diffing
// an id-keyed set for this.
export function setLines(id, horizontalLines, verticalLines, diagonalLines, rangeFills) {
    const entry = instances.get(id);
    if (!entry) return;

    for (const line of entry.horizontalLineObjs) entry.candleSeries.removePriceLine(line);
    entry.horizontalLineObjs = horizontalLines.map(l => entry.candleSeries.createPriceLine({
        price: l.price,
        color: colorFor(entry, l.colorGroup),
        lineWidth: 1,
        lineStyle: LightweightCharts.LineStyle.Dashed,
        axisLabelVisible: true,
        title: l.label || '',
    }));

    entry.verticalLines.setLines(verticalLines.map(l => ({ time: l.time, color: colorFor(entry, l.colorGroup) })));
    entry.diagonalLines.setLines((diagonalLines || []).map(l => (
        { time1: l.time1, price1: l.price1, time2: l.time2, price2: l.price2, color: colorFor(entry, l.colorGroup) })));
    entry.rangeFillsRaw = rangeFills || [];
    entry.volumeRangeFill = entry.rangeFillsRaw.find(f => f.colorGroup === 'volume') || null;
    entry.rangeFills.setFills(entry.rangeFillsRaw.map(f => (
        { time1: f.time1, time2: f.time2, label: f.label, color: colorFor(entry, f.colorGroup) })));
}

// markers: [{ time, shape, colorGroup, text }] -- shape is a lightweight-charts
// series-marker shape ('arrowUp'/'arrowDown'/'circle'/...), position is derived
// from it here (arrowUp sits belowBar so it points up *at* the bar, arrowDown
// sits aboveBar, everything else sits inBar) rather than being caller-supplied,
// since every current shape maps to exactly one sensible placement. Same
// clear-and-redraw-all approach as setLines -- one episode's worth of decision
// markers is a small, bounded count.
function markerPosition(shape) {
    if (shape === 'arrowUp') return 'belowBar';
    if (shape === 'arrowDown') return 'aboveBar';
    return 'inBar';
}

export function setActionMarkers(id, markers) {
    const entry = instances.get(id);
    if (!entry) return;

    entry.actionMarkers.setMarkers((markers || []).map(m => ({
        time: m.time,
        position: markerPosition(m.shape),
        shape: m.shape,
        color: colorFor(entry, m.colorGroup),
        text: m.text || '',
    })));
}

// Releases the pendingLoadMore guard set in create()'s subscribeVisibleLogicalRangeChange
// handler -- called by MarketChart.OnNearStartAsync once its round trip to .NET
// finishes, whether or not that actually resulted in a setData call.
export function resumeLoadMore(id) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.pendingLoadMore = false;
}

export function dispose(id) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.chart.remove();
    entry.tooltip.remove();
    if (entry.minimap) {
        entry.minimap.resizeObserver?.disconnect();
        entry.minimap.canvas.remove();
    }
    instances.delete(id);
}
