// Canvas renderer for CompareTxFold.razor.cs: one group per trailing timeframe, each holding every
// symbol's bar as % change from that symbol's price at the anchor -- overlaid at partial opacity or
// side by side. Up bars are filled and down bars hollow, so direction reads in a single colour.
import { cssColor, withAlpha } from './chartColors.js';

const PADDING = { top: 12, right: 58, bottom: 22, left: 8 };
const GROUP_FILL = 0.72;
const MAX_BAR_WIDTH = 28;
const OVERLAID_BODY_OPACITY = 0.45;
const SIDE_BY_SIDE_BODY_OPACITY = 0.85;
const OUTLINE_OPACITY = 0.9;
const PARTIAL_COVERAGE_ALPHA = 0.4;
const TEXT_COLOR = '#c9c9c9';
const FONT = '10px monospace';

const state = new Map();

export function render(canvas, options) {
    let entry = state.get(canvas);
    if (!entry) {
        entry = { hover: null, tooltip: createTooltip(canvas) };
        entry.resizeObserver = new ResizeObserver(() => draw(canvas));
        entry.resizeObserver.observe(canvas);
        canvas.addEventListener('mousemove', e => onHover(canvas, e));
        canvas.addEventListener('mouseleave', () => {
            entry.hover = null;
            entry.tooltip.style.display = 'none';
            draw(canvas);
        });
        state.set(canvas, entry);
    }

    entry.layout = options.layout;
    entry.groups = Array.from(options.groups ?? []);
    entry.series = Array.from(options.series ?? []).map(s => ({
        label: s.label,
        color: cssColor(canvas, s.colorVar),
        bySeconds: new Map(Array.from(s.bars).map(b => [b.seconds, b])),
    }));
    draw(canvas);
}

export function dispose(canvas) {
    const entry = state.get(canvas);
    if (!entry) return;
    entry.resizeObserver.disconnect();
    entry.tooltip.remove();
    state.delete(canvas);
}

function createTooltip(canvas) {
    const tooltip = document.createElement('div');
    tooltip.style.cssText =
        'display:none;position:absolute;pointer-events:none;z-index:5;' +
        'background:rgba(10,10,10,0.9);border:1px solid rgba(255,255,255,0.15);border-radius:4px;' +
        'padding:6px 8px;font-size:0.72rem;font-family:monospace;color:#e6e6e6;white-space:nowrap;';
    canvas.parentElement?.appendChild(tooltip);
    return tooltip;
}

function geometry(canvas, entry) {
    const width = canvas.clientWidth || 400;
    const height = canvas.clientHeight || 240;
    const plotLeft = PADDING.left;
    const plotRight = width - PADDING.right;
    const plotTop = PADDING.top;
    const plotBottom = height - PADDING.bottom;

    const values = entry.series.flatMap(s => [...s.bySeconds.values()].flatMap(b => [b.low, b.high]));
    let min = Math.min(0, ...values);
    let max = Math.max(0, ...values);
    if (min === max) { min -= 0.05; max += 0.05; }
    const pad = (max - min) * 0.08;
    min -= pad;
    max += pad;

    const y = v => plotTop + ((max - v) / (max - min)) * (plotBottom - plotTop);
    const groupWidth = (plotRight - plotLeft) / Math.max(1, entry.groups.length);
    return { width, height, plotLeft, plotRight, plotTop, plotBottom, min, max, y, groupWidth };
}

function niceStep(range, targetTicks) {
    const raw = range / targetTicks;
    const magnitude = 10 ** Math.floor(Math.log10(raw));
    const n = raw / magnitude;
    return (n < 1.5 ? 1 : n < 3 ? 2 : n < 7 ? 5 : 10) * magnitude;
}

function formatPercent(value, step) {
    const decimals = step >= 1 ? 1 : step >= 0.1 ? 2 : 3;
    return `${value.toFixed(decimals)}%`;
}

function barSlots(entry, g, groupIndex) {
    const left = g.plotLeft + groupIndex * g.groupWidth;
    const inner = g.groupWidth * GROUP_FILL;
    if (entry.layout === 'sidebyside') {
        const slot = inner / Math.max(1, entry.series.length);
        const start = left + (g.groupWidth - inner) / 2;
        return entry.series.map((_, i) => ({ x: start + slot * (i + 0.5), width: Math.max(1, Math.min(MAX_BAR_WIDTH, slot * 0.8)) }));
    }
    return entry.series.map(() => ({ x: left + g.groupWidth / 2, width: Math.max(1, Math.min(MAX_BAR_WIDTH, inner * 0.6)) }));
}

function drawBar(ctx, g, bar, slot, color, bodyOpacity) {
    const coverage = bar.hasFullCoverage ? 1 : PARTIAL_COVERAGE_ALPHA;
    const outline = withAlpha(color, OUTLINE_OPACITY * coverage);
    const x = Math.round(slot.x) + 0.5;
    const half = slot.width / 2;
    const top = g.y(Math.max(bar.open, bar.close));
    const bodyHeight = Math.max(1, g.y(Math.min(bar.open, bar.close)) - top);

    ctx.strokeStyle = outline;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(x, g.y(bar.high));
    ctx.lineTo(x, g.y(bar.low));
    ctx.stroke();

    if (bar.close >= bar.open) {
        ctx.fillStyle = withAlpha(color, bodyOpacity * coverage);
        ctx.fillRect(x - half, top, slot.width, bodyHeight);
    }
    ctx.strokeRect(x - half, top, slot.width, bodyHeight);
}

function draw(canvas) {
    const entry = state.get(canvas);
    if (!entry) return;

    const g = geometry(canvas, entry);
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(g.width * dpr));
    canvas.height = Math.max(1, Math.round(g.height * dpr));
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, g.width, g.height);
    ctx.font = FONT;

    if (entry.hover !== null) {
        ctx.fillStyle = 'rgba(255,255,255,0.05)';
        ctx.fillRect(g.plotLeft + entry.hover * g.groupWidth, g.plotTop, g.groupWidth, g.plotBottom - g.plotTop);
    }

    const step = niceStep(g.max - g.min, 6);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    for (let v = Math.ceil(g.min / step) * step; v <= g.max; v += step) {
        const y = Math.round(g.y(v)) + 0.5;
        const isZero = Math.abs(v) < step / 2;
        ctx.strokeStyle = isZero ? 'rgba(255,255,255,0.35)' : 'rgba(255,255,255,0.06)';
        ctx.beginPath();
        ctx.moveTo(g.plotLeft, y);
        ctx.lineTo(g.plotRight, y);
        ctx.stroke();
        ctx.fillStyle = TEXT_COLOR;
        ctx.fillText(formatPercent(isZero ? 0 : v, step), g.plotRight + 6, y);
    }

    const bodyOpacity = entry.layout === 'sidebyside' ? SIDE_BY_SIDE_BODY_OPACITY : OVERLAID_BODY_OPACITY;
    entry.groups.forEach((group, groupIndex) => {
        if (groupIndex > 0) {
            const x = Math.round(g.plotLeft + groupIndex * g.groupWidth) + 0.5;
            ctx.strokeStyle = 'rgba(255,255,255,0.04)';
            ctx.beginPath();
            ctx.moveTo(x, g.plotTop);
            ctx.lineTo(x, g.plotBottom);
            ctx.stroke();
        }

        const slots = barSlots(entry, g, groupIndex);
        entry.series.forEach((series, i) => {
            const bar = series.bySeconds.get(group.seconds);
            if (bar) drawBar(ctx, g, bar, slots[i], series.color, bodyOpacity);
        });

        ctx.fillStyle = TEXT_COLOR;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'top';
        ctx.fillText(group.label, g.plotLeft + (groupIndex + 0.5) * g.groupWidth, g.plotBottom + 6);
    });
}

function onHover(canvas, e) {
    const entry = state.get(canvas);
    if (!entry || entry.groups.length === 0) return;

    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const g = geometry(canvas, entry);
    const index = Math.floor((x - g.plotLeft) / g.groupWidth);
    const hover = index >= 0 && index < entry.groups.length && x <= g.plotRight ? index : null;

    if (hover !== entry.hover) {
        entry.hover = hover;
        draw(canvas);
    }
    if (hover === null) {
        entry.tooltip.style.display = 'none';
        return;
    }

    const group = entry.groups[hover];
    const f = v => `${v.toFixed(3)}%`;
    const rows = entry.series.map(series => {
        const bar = series.bySeconds.get(group.seconds);
        const values = bar
            ? `O ${f(bar.open)} H ${f(bar.high)} L ${f(bar.low)} C ${f(bar.close)}${bar.hasFullCoverage ? '' : ' (partial)'}`
            : 'no data';
        return `<div style="color:${series.color}">${series.label} ${values}</div>`;
    });
    entry.tooltip.innerHTML = `<div>${group.label} trailing window</div>${rows.join('')}`;
    entry.tooltip.style.display = 'block';

    const container = canvas.parentElement;
    const left = Math.min(x + 12, (container?.clientWidth ?? g.width) - entry.tooltip.offsetWidth - 8);
    entry.tooltip.style.left = `${Math.max(left, 8)}px`;
    entry.tooltip.style.top = `${Math.max(y - 12, 8)}px`;
}
