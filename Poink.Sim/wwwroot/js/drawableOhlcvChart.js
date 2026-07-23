// Candlestick + volume chart for ProfilerPage: click-to-draw horizontal/vertical/
// diagonal lines (plus T-Range/P-Range/Rectangle, built from placing 2 or 2+2 of the
// single-axis lines -- see modeClickCounts), plus pan-triggered history backfill.
// Deliberately a separate, independent module from ohlcvChart.js (not a shared/
// refactored one) -- see DrawableOhlcvChart.razor.cs's class comment for why
// OhlcvChart itself stays untouched rather than growing these Profiler-specific behaviors.
const instances = new Map();

const upColor = '#4dff88';
const downColor = '#ff5f56';

// How close (in bars) the visible left edge has to get to the start of the
// currently loaded data before a backfill request fires -- matches the threshold
// used by lightweight-charts' own infinite-history examples.
const nearStartThreshold = 10;

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

// How many chart clicks each draw mode needs before it's complete and dispatched to
// .NET (see the subscribeClick handler in create() below) -- T-Range/P-Range/Rectangle/
// T-Range-Chart place plain horizontal/vertical lines under the hood (2, 2, 2+2, and 2
// respectively), not a shape of their own, so they need no entry in
// createDiagonalLinesPrimitive or setLines below, unlike 'diagonal'.
const modeClickCounts = { horizontal: 1, vertical: 1, diagonal: 2, trange: 2, prange: 2, rectangle: 4, trangechart: 2 };

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

// minimapContainer: DrawableOhlcvChart.razor's .doc-minimap div -- the canvas
// itself is created here (not in .razor markup) since it needs no Blazor-side
// state, same reasoning as the tooltip element below.
function createMinimap(minimapContainer) {
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'display:block;width:100%;height:100%;';
    minimapContainer.appendChild(canvas);

    const minimap = { canvas, container: minimapContainer, dragMode: null, dragStartX: 0, dragStartRange: null };
    return minimap;
}

export function create(container, minimapContainer, dotNetRef) {
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
        crosshair: { mode: LightweightCharts.CrosshairMode.Normal },
        // Reacts to the container's own size (via ResizeObserver) so the expand/collapse
        // toggle in DrawableOhlcvChart.razor.cs needs no explicit resize call from .NET.
        autoSize: true,
    });

    const candleSeries = chart.addSeries(LightweightCharts.CandlestickSeries, {
        upColor, downColor, borderVisible: false, wickUpColor: upColor, wickDownColor: downColor,
        priceLineVisible: false, lastValueVisible: false,
    });

    const volumeSeries = chart.addSeries(LightweightCharts.HistogramSeries, {
        priceFormat: { type: 'volume' },
    }, 1);
    chart.panes()[0].setStretchFactor(3);
    chart.panes()[1].setStretchFactor(1);

    const tooltip = document.createElement('div');
    tooltip.style.cssText =
        'display:none;position:absolute;pointer-events:none;z-index:5;' +
        'background:rgba(10,10,10,0.9);border:1px solid rgba(255,255,255,0.15);border-radius:4px;' +
        'padding:6px 8px;font-size:0.75rem;font-family:monospace;color:#e6e6e6;white-space:nowrap;';
    container.style.position = 'relative';
    container.appendChild(tooltip);

    chart.subscribeCrosshairMove(param => {
        const bar = param.time ? param.seriesData.get(candleSeries) : undefined;
        if (!param.point || param.point.x < 0 || param.point.y < 0 || !bar) {
            tooltip.style.display = 'none';
            return;
        }

        const vol = param.seriesData.get(volumeSeries);
        const d = new Date(param.time * 1000);
        tooltip.innerHTML =
            `<div>${d.toISOString().slice(0, 16).replace('T', ' ')}</div>` +
            `<div>O ${bar.open.toFixed(2)}  H ${bar.high.toFixed(2)}  L ${bar.low.toFixed(2)}  C ${bar.close.toFixed(2)}</div>` +
            (vol ? `<div>Vol ${vol.value.toLocaleString()}</div>` : '');

        tooltip.style.display = 'block';
        const left = Math.min(param.point.x + 12, container.clientWidth - tooltip.offsetWidth - 8);
        tooltip.style.left = `${Math.max(left, 8)}px`;
        tooltip.style.top = `${Math.max(param.point.y - 12, 8)}px`;
    });

    // Same custom properties DrawableOhlcvChart.razor.css's .doc-tool-btn-* group
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
    };

    const verticalLines = createVerticalLinesPrimitive();
    candleSeries.attachPrimitive(verticalLines);
    const diagonalLines = createDiagonalLinesPrimitive();
    candleSeries.attachPrimitive(diagonalLines);
    const rangeFills = createRangeFillsPrimitive();
    candleSeries.attachPrimitive(rangeFills);

    const id = crypto.randomUUID();
    const entry = {
        chart, candleSeries, volumeSeries, tooltip, verticalLines, diagonalLines, rangeFills, accentColor, groupColors,
        horizontalLineObjs: [], dotNetRef, hasData: false, pendingLoadMore: false,
        drawMode: null, drawClicks: [], bars: [], minimap: null,
    };
    instances.set(id, entry);

    entry.minimap = createMinimap(minimapContainer);
    attachMinimapInteractions(entry);
    const minimapResizeObserver = new ResizeObserver(() => resizeMinimapCanvas(entry));
    minimapResizeObserver.observe(minimapContainer);
    entry.minimap.resizeObserver = minimapResizeObserver;
    resizeMinimapCanvas(entry);

    // Infinite-history backfill: once the visible range gets within
    // nearStartThreshold bars of the oldest loaded bar, ask .NET (ProfilerPage, via
    // DrawableOhlcvChart.OnNearStartAsync) for the next older chunk. pendingLoadMore
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

        entry.drawClicks.push({ time, price });
        const mode = entry.drawMode;
        const clickIndex = entry.drawClicks.length;
        const ref = entry.dotNetRef;

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
        }

        const needed = modeClickCounts[mode] || 1;
        if (entry.drawClicks.length < needed) return;

        if (mode === 'diagonal') {
            const clicks = entry.drawClicks;
            ref.invokeMethodAsync(
                'OnDiagonalLineClickedAsync', clicks[0].price, clicks[0].time, clicks[1].price, clicks[1].time);
        } else if (mode === 'trangechart') {
            const clicks = entry.drawClicks;
            ref.invokeMethodAsync('OnTRangeChartClickedAsync', clicks[0].time, clicks[1].time);
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

    entry.candleSeries.setData(bars.map(b => ({ time: b.time, open: b.open, high: b.high, low: b.low, close: b.close })));
    entry.volumeSeries.setData(bars.map(b => ({
        time: b.time, value: b.volume, color: b.close >= b.open ? upColor : downColor,
    })));

    entry.bars = bars;
    entry.hasData = bars.length > 0;
    if (resetView) entry.chart.timeScale().fitContent();
    drawMinimap(entry);
}

// mode: one of modeClickCounts' keys, or null (null/falsy disarms drawing entirely).
// Also resets any in-progress multi-click sequence (e.g. Diagonal's first click, or
// T-Range/Rectangle's partial set of points) -- switching tools or re-clicking the
// same toolbar button to cancel shouldn't leave stale clicks for the next placement.
export function setDrawMode(id, mode) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.drawMode = mode || null;
    entry.drawClicks = [];
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
    entry.rangeFills.setFills((rangeFills || []).map(f => (
        { time1: f.time1, time2: f.time2, label: f.label, color: colorFor(entry, f.colorGroup) })));
}

// Releases the pendingLoadMore guard set in create()'s subscribeVisibleLogicalRangeChange
// handler -- called by DrawableOhlcvChart.OnNearStartAsync once its round trip to .NET
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
