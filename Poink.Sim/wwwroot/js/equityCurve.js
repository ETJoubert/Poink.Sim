// Read-only equity-over-time chart: a solid trace for equity plus two fainter traces for the
// best/worst equity reached during the currently open trade. A null sample breaks its trace into
// a separate path segment rather than drawing through it, so gaps between trades don't render as
// a connecting line. Per-instance state keyed by the container element itself, same WeakMap
// convention as marketChartSparkline.js.
const state = new WeakMap();

function segments(samples, pick) {
    const result = [];
    let current = null;
    samples.forEach((s, i) => {
        const v = pick(s);
        if (v == null) {
            current = null;
            return;
        }
        if (!current) {
            current = [];
            result.push(current);
        }
        current.push({ i, v });
    });
    return result;
}

function strokeSeries(ctx, samples, pick, xForIndex, yForValue, color, alpha, lineWidth, dash) {
    const segs = segments(samples, pick);
    if (segs.length === 0) return;

    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = color;
    ctx.lineWidth = lineWidth;
    if (dash) ctx.setLineDash(dash);
    for (const seg of segs) {
        if (seg.length < 2) continue;
        const path = new Path2D();
        seg.forEach((p, idx) => {
            const x = xForIndex(p.i);
            const y = yForValue(p.v);
            if (idx === 0) path.moveTo(x, y); else path.lineTo(x, y);
        });
        ctx.stroke(path);
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
            if (v == null) continue;
            if (v < min) min = v;
            if (v > max) max = v;
        }
    }
    if (!isFinite(min) || !isFinite(max)) return;
    if (min === max) { min -= 1; max += 1; }

    const padY = rect.height * 0.1;
    const plotH = rect.height - padY * 2;
    const yForValue = v => padY + plotH - ((v - min) / (max - min)) * plotH;
    const xForIndex = i => (i / Math.max(1, samples.length - 1)) * rect.width;

    const style = getComputedStyle(container);
    const accentColor = style.getPropertyValue('--color-accent').trim() || '#ff6b9d';
    const winColor = style.getPropertyValue('--color-success').trim() || '#2ecc71';
    const lossColor = style.getPropertyValue('--color-error').trim() || '#e74c3c';

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
