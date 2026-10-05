// Multi-series comparison chart driven by CompareChart.razor.cs: one line or candle series per
// series on a shared time axis, an optional lower pane (pane index 1, e.g. a ratio line) and a
// shaded Lookback Window. Per-instance state is keyed by the container element.
import { cssColor, withAlpha } from './chartColors.js';

const instances = new WeakMap();

const nearStartThreshold = 50;
const leftOffsetBars = 10;
const percentFormat = { type: 'custom', minMove: 0.01, formatter: v => `${v.toFixed(2)}%` };
const barBodyOpacity = 0.45;
const barOutlineOpacity = 0.85;

// Drawn behind the lines (drawBackground) so the window highlights them instead of covering them.
function createWindowFillPrimitive(color) {
    let band = null;
    let chart = null;
    let requestUpdate = null;

    const renderer = {
        draw() { },
        drawBackground(target) {
            if (!band || !chart) return;
            const x1 = chart.timeScale().timeToCoordinate(band.start);
            const x2 = chart.timeScale().timeToCoordinate(band.end);
            if (x1 === null || x2 === null) return;
            target.useMediaCoordinateSpace(scope => {
                const ctx = scope.context;
                const left = Math.min(x1, x2);
                const width = Math.max(1, Math.abs(x2 - x1));
                ctx.save();
                ctx.globalAlpha = 0.12;
                ctx.fillStyle = color;
                ctx.fillRect(left, 0, width, scope.mediaSize.height);
                ctx.globalAlpha = 0.7;
                ctx.strokeStyle = color;
                ctx.setLineDash([4, 4]);
                ctx.beginPath();
                ctx.moveTo(x1 + 0.5, 0);
                ctx.lineTo(x1 + 0.5, scope.mediaSize.height);
                ctx.moveTo(x2 + 0.5, 0);
                ctx.lineTo(x2 + 0.5, scope.mediaSize.height);
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
        updateAllViews() { },
        paneViews: () => [paneView],
        setWindow(newBand) {
            band = newBand;
            if (requestUpdate) requestUpdate();
        },
    };
}

function formatValue(entry, pane, value) {
    return pane === 0 ? `${value.toFixed(2)}%` : value.toFixed(5);
}

function firstIndexAtOrAfter(points, time) {
    let lo = 0, hi = points.length;
    while (lo < hi) {
        const mid = (lo + hi) >> 1;
        if (points[mid].time < time) lo = mid + 1; else hi = mid;
    }
    return lo;
}

function toPercent(point, base, kind) {
    const pct = v => (v / base - 1) * 100;
    return kind === 'bars'
        ? { time: point.time, open: pct(point.open), high: pct(point.high), low: pct(point.low), close: pct(point.close) }
        : { time: point.time, value: pct(point.value) };
}

function baseValue(point, kind) {
    return kind === 'bars' ? point.close : point.value;
}

// Rebased mode converts raw main-pane values to % here rather than via the library's Percentage
// scale, so lines and candles share one % axis; force re-applies after the raw data changes.
function rebaseMainPane(entry, force) {
    if (entry.scaleMode !== 'rebased') return;
    const visible = entry.chart.timeScale().getVisibleRange();
    for (const item of entry.series.values()) {
        if (item.pane !== 0 || item.raw.length === 0) continue;
        const index = visible ? Math.min(firstIndexAtOrAfter(item.raw, visible.from), item.raw.length - 1) : 0;
        const base = baseValue(item.raw[index], item.kind);
        if (!force && base === item.base) continue;
        item.base = base;
        item.series.setData(base === 0 ? [] : item.raw.map(p => toPercent(p, base, item.kind)));
    }
}

function formatPoint(entry, item, point) {
    const f = v => formatValue(entry, item.pane, v);
    return item.kind === 'bars'
        ? `O ${f(point.open)} H ${f(point.high)} L ${f(point.low)} C ${f(point.close)}`
        : f(point.value);
}

function barColors(color) {
    const body = withAlpha(color, barBodyOpacity);
    const outline = withAlpha(color, barOutlineOpacity);
    return {
        upColor: body, downColor: 'rgba(0,0,0,0)',
        borderVisible: true, borderUpColor: outline, borderDownColor: outline,
        wickUpColor: outline, wickDownColor: outline,
    };
}

function addSeries(entry, s, color) {
    const common = { title: s.label, priceLineVisible: false, lastValueVisible: true };
    return s.kind === 'bars'
        ? entry.chart.addSeries(LightweightCharts.CandlestickSeries, { ...common, ...barColors(color) }, s.pane)
        : entry.chart.addSeries(LightweightCharts.LineSeries, {
            ...common, color, lineWidth: s.pane === 0 ? 2 : 1, crosshairMarkerVisible: true,
        }, s.pane);
}

function attachTooltip(container, entry) {
    const tooltip = document.createElement('div');
    tooltip.style.cssText =
        'display:none;position:absolute;pointer-events:none;z-index:5;' +
        'background:rgba(10,10,10,0.9);border:1px solid rgba(255,255,255,0.15);border-radius:4px;' +
        'padding:6px 8px;font-size:0.75rem;font-family:monospace;color:#e6e6e6;white-space:nowrap;';
    container.style.position = 'relative';
    container.appendChild(tooltip);

    entry.chart.subscribeCrosshairMove(param => {
        if (!param.point || !param.time || param.point.x < 0 || param.point.y < 0) {
            tooltip.style.display = 'none';
            return;
        }

        const rows = [];
        for (const item of entry.series.values()) {
            const point = param.seriesData.get(item.series);
            if (point) rows.push(`<div style="color:${item.color}">${item.label} ${formatPoint(entry, item, point)}</div>`);
        }
        if (rows.length === 0) {
            tooltip.style.display = 'none';
            return;
        }

        const time = new Date(param.time * 1000).toISOString().slice(0, 16).replace('T', ' ');
        tooltip.innerHTML = `<div>${time}</div>${rows.join('')}`;
        tooltip.style.display = 'block';
        const left = Math.min(param.point.x + 12, container.clientWidth - tooltip.offsetWidth - 8);
        tooltip.style.left = `${Math.max(left, 8)}px`;
        tooltip.style.top = `${Math.max(param.point.y - 12, 8)}px`;
    });
}

export function create(container, dotNetRef) {
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
        autoSize: true,
    });

    // The library drops a pane once its last series is removed, which would take the window fill with it.
    const windowFill = createWindowFillPrimitive(cssColor(container, '--color-info'));
    chart.panes()[0].setPreserveEmptyPane(true);
    chart.panes()[0].attachPrimitive(windowFill);

    const entry = {
        chart, dotNetRef, windowFill, container,
        series: new Map(), scaleMode: 'rebased', hasData: false, canLoadMore: false, pendingLoadMore: false,
    };
    instances.set(container, entry);
    attachTooltip(container, entry);

    chart.timeScale().subscribeVisibleLogicalRangeChange(range => {
        if (!entry.rebaseQueued) {
            entry.rebaseQueued = true;
            requestAnimationFrame(() => {
                entry.rebaseQueued = false;
                if (instances.has(container)) rebaseMainPane(entry, false);
            });
        }
        if (!range || !entry.hasData || !entry.canLoadMore || entry.pendingLoadMore) return;
        if (range.from > nearStartThreshold) return;
        entry.pendingLoadMore = true;
        entry.dotNetRef.invokeMethodAsync('OnNearStartAsync').finally(() => { entry.pendingLoadMore = false; });
    });
}

function applyPercentAxis(item) {
    if (item.pane !== 0) return;
    item.series.applyOptions({ priceFormat: percentFormat });
}

// seriesList: [{ key, kind: 'line' | 'bars', label, colorVar, pane, points: [{ time, value } | { time, open, high, low, close }] }],
// scaleMode: 'rebased' | 'percentvalues'.
export function setSeries(container, seriesList, scaleMode) {
    const entry = instances.get(container);
    if (!entry) return;
    entry.scaleMode = scaleMode;

    for (const s of seriesList) {
        let item = entry.series.get(s.key);
        if (!item) {
            const color = cssColor(container, s.colorVar);
            item = { series: addSeries(entry, s, color), kind: s.kind, label: s.label, color, pane: s.pane };
            entry.series.set(s.key, item);
        }
        applyPercentAxis(item);
        item.raw = s.points;
        if (item.pane !== 0 || scaleMode !== 'rebased') item.series.setData(s.points);
    }

    // Removed only after the replacements are added, so a Lines/Bars switch never empties a pane and re-indexes the rest.
    const keys = new Set(seriesList.map(s => s.key));
    for (const [key, item] of entry.series) {
        if (keys.has(key)) continue;
        entry.chart.removeSeries(item.series);
        entry.series.delete(key);
    }
    rebaseMainPane(entry, true);

    const panes = entry.chart.panes();
    if (panes.length > 1 && entry.paneCount !== panes.length) {
        panes[0].setStretchFactor(3);
        panes[1].setStretchFactor(1);
    }
    entry.paneCount = panes.length;

    const hasData = seriesList.some(s => s.points.length > 0);
    if (hasData && !entry.hasData) {
        const ts = entry.chart.timeScale();
        ts.fitContent();
        const range = ts.getVisibleLogicalRange();
        if (range) ts.setVisibleLogicalRange({ from: range.from - leftOffsetBars, to: range.to });
    }
    entry.hasData = hasData;
}

export function setWindow(container, start, end) {
    const entry = instances.get(container);
    if (!entry) return;
    entry.windowFill.setWindow(start !== null && end !== null ? { start, end } : null);
}

export function setCanLoadMore(container, canLoadMore) {
    const entry = instances.get(container);
    if (!entry) return;
    entry.canLoadMore = canLoadMore;
}

export function dispose(container) {
    const entry = instances.get(container);
    if (!entry) return;
    entry.chart.remove();
    instances.delete(container);
}
