// Read-only equity-by-trade chart: one point per trade. The solid trace joins each trade's outcome
// equity; two fainter traces join the best/worst equity reached while that trade was open. Y axis
// reads equity; X axis reads trade number. Per-instance state keyed by the container element
// itself, same WeakMap convention as marketChartSparkline.js.
const state = new WeakMap();

const marginLeft = 52;
const marginBottom = 20;
const marginTop = 8;
const marginRight = 10;

function strokeSeries(ctx, samples, pick, xForIndex, yForValue, color, alpha, lineWidth, dash) {
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = color;
    ctx.fillStyle = color;
    ctx.lineWidth = lineWidth;
    if (dash) ctx.setLineDash(dash);

    const path = new Path2D();
    samples.forEach((s, i) => {
        const x = xForIndex(i);
        const y = yForValue(pick(s));
        if (i === 0) path.moveTo(x, y); else path.lineTo(x, y);
    });
    ctx.stroke(path);

    ctx.setLineDash([]);
    for (let i = 0; i < samples.length; i++) {
        ctx.beginPath();
        ctx.arc(xForIndex(i), yForValue(pick(samples[i])), lineWidth + 0.5, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.restore();
}

// Evenly spaced equity values to label on the Y axis -- not "nice rounded" numbers, just a plain
// even split of the visible range, since equity has no natural round-number scale to snap to.
function yAxisTicks(min, max, count) {
    const ticks = [];
    for (let i = 0; i < count; i++) ticks.push(min + ((max - min) * i) / (count - 1));
    return ticks;
}

function formatEquity(v) {
    const abs = Math.abs(v);
    const decimals = abs >= 1000 ? 0 : 2;
    return v.toLocaleString(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
}

// One tick per trade sample, thinned to every nth so labels stay legible on long runs.
function tradeAxisTicks(samples, xForIndex, plotW) {
    const minLabelSpacing = 28;
    const step = Math.max(1, Math.ceil((samples.length * minLabelSpacing) / Math.max(1, plotW)));
    const ticks = [];
    samples.forEach((s, i) => {
        if (i % step === 0) ticks.push({ index: i, number: s.tradeNumber });
    });
    return ticks;
}

function drawAxes(ctx, rect, samples, min, max, xForIndex, yForValue, gridColor, textColor) {
    const plotW = rect.width - marginLeft - marginRight;
    ctx.save();
    ctx.strokeStyle = gridColor;
    ctx.fillStyle = textColor;
    ctx.font = '10px sans-serif';
    ctx.lineWidth = 1;

    ctx.globalAlpha = 0.25;
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    for (const tick of yAxisTicks(min, max, 4)) {
        const y = Math.round(yForValue(tick)) + 0.5;
        ctx.beginPath();
        ctx.moveTo(marginLeft, y);
        ctx.lineTo(rect.width - marginRight, y);
        ctx.stroke();
    }

    ctx.globalAlpha = 0.8;
    for (const tick of yAxisTicks(min, max, 4)) {
        const y = yForValue(tick);
        ctx.fillText(formatEquity(tick), marginLeft - 6, y);
    }

    const axisY = rect.height - marginBottom;
    ctx.globalAlpha = 0.35;
    ctx.beginPath();
    ctx.moveTo(marginLeft, axisY + 0.5);
    ctx.lineTo(rect.width - marginRight, axisY + 0.5);
    ctx.stroke();

    ctx.globalAlpha = 0.8;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    for (const tick of tradeAxisTicks(samples, xForIndex, plotW)) {
        const x = xForIndex(tick.index);
        ctx.beginPath();
        ctx.moveTo(x + 0.5, axisY);
        ctx.lineTo(x + 0.5, axisY + 4);
        ctx.stroke();
        ctx.fillText(String(tick.number), x, axisY + 6);
    }

    ctx.restore();
}

function draw(container) {
    const s = state.get(container);
    if (!s) return;

    const { canvas, samples } = s;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));

    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, rect.width, rect.height);

    if (!samples || samples.length === 0 || rect.width === 0 || rect.height === 0) return;

    let min = Infinity, max = -Infinity;
    for (const p of samples) {
        for (const v of [p.equity, p.tradeMax, p.tradeMin]) {
            if (v < min) min = v;
            if (v > max) max = v;
        }
    }
    if (!isFinite(min) || !isFinite(max)) return;
    if (min === max) { min -= 1; max += 1; }

    const plotW = rect.width - marginLeft - marginRight;
    const plotH = rect.height - marginTop - marginBottom;
    const yForValue = v => marginTop + plotH - ((v - min) / (max - min)) * plotH;
    const xForIndex = i => marginLeft + (i / Math.max(1, samples.length - 1)) * plotW;

    const style = getComputedStyle(container);
    const accentColor = style.getPropertyValue('--color-accent').trim() || '#ff6b9d';
    const winColor = style.getPropertyValue('--color-success').trim() || '#2ecc71';
    const lossColor = style.getPropertyValue('--color-error').trim() || '#e74c3c';
    const gridColor = style.getPropertyValue('--color-text').trim() || '#ffffff';
    const textColor = gridColor;

    drawAxes(ctx, rect, samples, min, max, xForIndex, yForValue, gridColor, textColor);

    strokeSeries(ctx, samples, p => p.tradeMax, xForIndex, yForValue, winColor, 0.45, 1, [3, 3]);
    strokeSeries(ctx, samples, p => p.tradeMin, xForIndex, yForValue, lossColor, 0.45, 1, [3, 3]);
    strokeSeries(ctx, samples, p => p.equity, xForIndex, yForValue, accentColor, 0.85, 1.5, null);
}

export function create(container) {
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'display:block;width:100%;height:100%;';
    container.appendChild(canvas);

    const s = { canvas, samples: [] };
    state.set(container, s);

    const resizeObserver = new ResizeObserver(() => draw(container));
    resizeObserver.observe(container);
    s.resizeObserver = resizeObserver;
}

export function setSamples(container, samples) {
    const s = state.get(container);
    if (!s) return;
    s.samples = samples;
    draw(container);
}

export function dispose(container) {
    const s = state.get(container);
    if (!s) return;
    s.resizeObserver.disconnect();
    s.canvas.remove();
    state.delete(container);
}
