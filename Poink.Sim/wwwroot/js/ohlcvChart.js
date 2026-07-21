// Thin wrapper around the vendored TradingView Lightweight Charts library
// (loaded globally as `LightweightCharts` via wwwroot/js/lib/lightweight-charts.standalone.production.js,
// referenced from App.razor) giving Poink.ML's pages pan/zoom and a hover
// crosshair tooltip, neither of which the shared, hand-rolled
// CandlestickChart canvas renderer in Poink.Shared.Client has. Scoped to
// this project only -- that shared component is left untouched for its
// other consumers.
const instances = new Map();

const upColor = '#4dff88';
const downColor = '#ff5f56';

// Series Primitives API (attachPrimitive/detachPrimitive) has no built-in "vertical
// line at a given time" the way createPriceLine() covers horizontal ones, so this is
// a small hand-rolled primitive: updateAllViews() is called by the chart itself right
// before every paint, which is where the time->x conversion happens; requestUpdate()
// (handed to us via attached()) is how we ask the library to repaint after the target
// time changes rather than only on the next pan/zoom/resize.
function createVerticalLinePrimitive(color) {
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

export function create(container) {
    const chart = LightweightCharts.createChart(container, {
        layout: { background: { type: 'solid', color: 'transparent' }, textColor: '#c9c9c9' },
        grid: {
            vertLines: { color: 'rgba(255,255,255,0.06)' },
            horzLines: { color: 'rgba(255,255,255,0.06)' },
        },
        rightPriceScale: { borderColor: 'rgba(255,255,255,0.15)' },
        // rightOffset leaves empty space (in bar widths) past the last/current bar instead
        // of it sitting flush against the price-scale edge -- fitContent() (called after
        // every setData) respects this gap rather than stretching the last bar into it.
        timeScale: { borderColor: 'rgba(255,255,255,0.15)', timeVisible: true, secondsVisible: false, rightOffset: 10 },
        crosshair: { mode: LightweightCharts.CrosshairMode.Normal },
        // Reacts to the container's own size (via ResizeObserver) so the expand/collapse
        // toggle in OhlcvChart.razor.cs needs no explicit resize call from .NET.
        autoSize: true,
    });

    const candleSeries = chart.addSeries(LightweightCharts.CandlestickSeries, {
        upColor, downColor, borderVisible: false, wickUpColor: upColor, wickDownColor: downColor,
        // Off by default: the library auto-draws a dashed "last close" line + axis label
        // on every series unless told otherwise, which would show alongside our own
        // bid/ask lines below as an unwanted third one.
        priceLineVisible: false, lastValueVisible: false,
    });

    const volumeSeries = chart.addSeries(LightweightCharts.HistogramSeries, {
        priceFormat: { type: 'volume' }, priceScaleId: 'volume',
    });
    volumeSeries.priceScale().applyOptions({ scaleMargins: { top: 0.82, bottom: 0 } });

    // Styled inline, not via a CSS class: Blazor's CSS isolation only scopes elements
    // declared in the component's own .razor markup, not ones a JS module creates at
    // runtime, so a scoped .razor.css rule would silently never match this element.
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

    const markersPrimitive = LightweightCharts.createSeriesMarkers(candleSeries, []);

    // Same accent color as the tick-window slider's thumb (SimulationPage.razor.css
    // .sim-window-slider::-webkit-slider-thumb) -- read from the CSS custom property
    // rather than hardcoded so it stays correct across themes (base.css defines a
    // different --color-accent per theme), tying the marker back to the control that
    // drives it at a glance.
    const accentColor = getComputedStyle(container).getPropertyValue('--color-accent').trim() || '#ff6b9d';
    const windowLine = createVerticalLinePrimitive(accentColor);
    candleSeries.attachPrimitive(windowLine);

    const id = crypto.randomUUID();
    instances.set(id, { chart, candleSeries, volumeSeries, markersPrimitive, tooltip, windowLine, bidLine: null, askLine: null });
    return id;
}

export function setData(id, bars, predicted) {
    const entry = instances.get(id);
    if (!entry) return;

    const realData = bars.map(b => ({ time: b.time, open: b.open, high: b.high, low: b.low, close: b.close }));
    entry.volumeSeries.setData(bars.map(b => ({
        time: b.time, value: b.volume, color: b.close >= b.open ? upColor : downColor,
    })));

    // Each model's (or the actual outcome's) predicted High/Low for the bar right after the
    // window -- appended as its own solid-body bar (open=Low, close=High) in the model's
    // color, one interval apart, with one empty interval skipped first to read as a
    // separator. Generic over whatever labels `predicted` carries (not hardcoded to any
    // specific model), with a marker above each bar naming it since its time slot is made
    // up, not a real future timestamp.
    const interval = bars.length >= 2 ? bars[bars.length - 1].time - bars[bars.length - 2].time : 60;
    const lastTime = bars.length > 0 ? bars[bars.length - 1].time : 0;

    const predictedData = (predicted || []).map((p, i) => ({
        time: lastTime + interval * (2 + i),
        open: p.low, high: p.high, low: p.low, close: p.high,
        color: p.color, borderColor: p.color, wickColor: p.color,
    }));

    entry.candleSeries.setData([...realData, ...predictedData]);

    entry.markersPrimitive.setMarkers(predictedData.map((d, i) => ({
        time: d.time, position: 'aboveBar', color: predicted[i].color, shape: 'circle', text: predicted[i].label,
    })));

    entry.chart.timeScale().fitContent();
}

// Live bid/ask as horizontal price lines pinned to the right axis -- updated in place
// via applyOptions rather than remove+recreate, since this fires on every tick.
export function setCurrentPrices(id, bid, ask) {
    const entry = instances.get(id);
    if (!entry) return;

    entry.bidLine = updatePriceLine(entry.candleSeries, entry.bidLine, bid, downColor, 'BID');
    entry.askLine = updatePriceLine(entry.candleSeries, entry.askLine, ask, upColor, 'ASK');
}

function updatePriceLine(series, line, price, color, title) {
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
        lineStyle: LightweightCharts.LineStyle.Dashed,
        axisLabelVisible: true,
    });
}

// Vertical marker for where the tick window (feeding the volume-profile panel)
// starts -- null hides it (e.g. once the whole buffer fits inside the window).
export function setWindowStart(id, unixSeconds) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.windowLine.setTime(unixSeconds ?? null);
}

export function dispose(id) {
    const entry = instances.get(id);
    if (!entry) return;
    entry.chart.remove();
    entry.tooltip.remove();
    instances.delete(id);
}
