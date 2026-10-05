// Multi-series comparison chart driven by CompareChart.razor.cs: one line per series on a
// shared time axis, an optional lower pane (pane index 1, e.g. a ratio line) and a shaded
// Lookback Window. Per-instance state is keyed by the container element.
const instances = new WeakMap();

const nearStartThreshold = 50;
const leftOffsetBars = 10;
const percentFormat = { type: 'custom', minMove: 0.01, formatter: v => `${v.toFixed(2)}%` };
const valueFormat = { type: 'price', precision: 5, minMove: 0.00001 };

function cssColor(container, varName) {
    return getComputedStyle(container).getPropertyValue(varName).trim() || '#c9c9c9';
}

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
    return pane === 0 && entry.scaleMode === 'percentvalues' ? `${value.toFixed(2)}%` : value.toFixed(5);
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
            if (point) rows.push(`<div style="color:${item.color}">${item.label} ${formatValue(entry, item.pane, point.value)}</div>`);
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

    const windowFill = createWindowFillPrimitive(cssColor(container, '--color-info'));
    chart.panes()[0].attachPrimitive(windowFill);

    const entry = {
        chart, dotNetRef, windowFill, container,
        series: new Map(), scaleMode: 'rebased', hasData: false, canLoadMore: false, pendingLoadMore: false,
    };
    instances.set(container, entry);
    attachTooltip(container, entry);

    chart.timeScale().subscribeVisibleLogicalRangeChange(range => {
        if (!range || !entry.hasData || !entry.canLoadMore || entry.pendingLoadMore) return;
        if (range.from > nearStartThreshold) return;
        entry.pendingLoadMore = true;
        entry.dotNetRef.invokeMethodAsync('OnNearStartAsync').finally(() => { entry.pendingLoadMore = false; });
    });
}

function applyScaleMode(entry, item) {
    if (item.pane !== 0) return;
    const percentValues = entry.scaleMode === 'percentvalues';
    item.series.applyOptions({ priceFormat: percentValues ? percentFormat : valueFormat });
    item.series.priceScale().applyOptions({
        mode: percentValues ? LightweightCharts.PriceScaleMode.Normal : LightweightCharts.PriceScaleMode.Percentage,
    });
}

// seriesList: [{ key, label, colorVar, pane, points: [{ time, value }] }], scaleMode: 'rebased' | 'percentvalues'.
export function setSeries(container, seriesList, scaleMode) {
    const entry = instances.get(container);
    if (!entry) return;
    entry.scaleMode = scaleMode;

    const keys = new Set(seriesList.map(s => s.key));
    for (const [key, item] of entry.series) {
        if (keys.has(key)) continue;
        entry.chart.removeSeries(item.series);
        entry.series.delete(key);
    }

    for (const s of seriesList) {
        let item = entry.series.get(s.key);
        if (!item) {
            const color = cssColor(container, s.colorVar);
            const series = entry.chart.addSeries(LightweightCharts.LineSeries, {
                color, lineWidth: s.pane === 0 ? 2 : 1, title: s.label,
                priceLineVisible: false, lastValueVisible: true, crosshairMarkerVisible: true,
            }, s.pane);
            item = { series, label: s.label, color, pane: s.pane };
            entry.series.set(s.key, item);
        }
        applyScaleMode(entry, item);
        item.series.setData(s.points);
    }

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
