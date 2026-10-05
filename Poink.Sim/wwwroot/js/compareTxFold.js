// Canvas renderer for CompareTxFold.razor.cs: one group per trailing timeframe, each holding every
// symbol's bar as % change from that symbol's price at the anchor -- overlaid at partial opacity or
// side by side. Up bars are filled and down bars hollow, so direction reads in a single colour.
import { cssColor, withAlpha } from './chartColors.js';
import { placeTooltip } from './chartTooltip.js';

const PADDING = { top: 12, right: 58, bottom: 22, left: 8 };
const GROUP_FILL = 0.72;
const MAX_BAR_WIDTH = 28;
const OVERLAID_BODY_OPACITY = 0.45;
const SIDE_BY_SIDE_BODY_OPACITY = 0.85;
const OUTLINE_OPACITY = 0.9;
const PARTIAL_COVERAGE_ALPHA = 0.4;
const PROFILE_GAP = 6;
const PROFILE_OPACITY = 0.4;
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
    entry.showVolumeProfile = !!options.showVolumeProfile;
    entry.groups = Array.from(options.groups ?? []);
    entry.series = Array.from(options.series ?? []).map(s => {
        const profile = Array.from(s.profile ?? []);
        return {
            label: s.label,
            color: cssColor(canvas, s.colorVar),
            bySeconds: new Map(Array.from(s.bars).map(b => [b.seconds, b])),
            profile,
            profileMax: Math.max(0, ...profile.map(p => p.count)),
        };
    });
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
    const available = width - PADDING.left - PADDING.right;
    const profileWidth = entry.showVolumeProfile ? Math.min(90, Math.max(30, available * 0.18)) : 0;
    const plotRight = width - PADDING.right - (entry.showVolumeProfile ? profileWidth + PROFILE_GAP : 0);
    const profileLeft = plotRight + PROFILE_GAP;
    const axisX = (entry.showVolumeProfile ? profileLeft + profileWidth : plotRight) + 6;
    const plotTop = PADDING.top;
    const plotBottom = height - PADDING.bottom;

    const values = entry.series.flatMap(s => [
        ...[...s.bySeconds.values()].flatMap(b => [b.low, b.high]),
        ...(entry.showVolumeProfile ? s.profile.flatMap(p => [p.low, p.high]) : []),
    ]);
    let min = Math.min(0, ...values);
    let max = Math.max(0, ...values);
    if (min === max) { min -= 0.05; max += 0.05; }
    const pad = (max - min) * 0.08;
    min -= pad;
    max += pad;

    const y = v => plotTop + ((max - v) / (max - min)) * (plotBottom - plotTop);
    const groupWidth = (plotRight - plotLeft) / Math.max(1, entry.groups.length);
    return { width, height, plotLeft, plotRight, plotTop, plotBottom, min, max, y, groupWidth, profileLeft, profileWidth, axisX };
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

// Each symbol's buckets are scaled to its own busiest bucket and overlaid, so a busier symbol doesn't flatten the rest.
function drawVolumeProfile(ctx, g, entry) {
    ctx.strokeStyle = 'rgba(255,255,255,0.12)';
    ctx.beginPath();
    ctx.moveTo(Math.round(g.profileLeft) + 0.5, g.plotTop);
    ctx.lineTo(Math.round(g.profileLeft) + 0.5, g.plotBottom);
    ctx.stroke();

    for (const series of entry.series) {
        if (series.profileMax <= 0) continue;
        ctx.fillStyle = withAlpha(series.color, PROFILE_OPACITY);
        for (const bucket of series.profile) {
            const top = g.y(bucket.high);
            ctx.fillRect(
                g.profileLeft + 1,
                top,
                Math.max(1, (bucket.count / series.profileMax) * (g.profileWidth - 1)),
                Math.max(1, g.y(bucket.low) - top),
            );
        }
    }
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
        ctx.fillText(formatPercent(isZero ? 0 : v, step), g.axisX, y);
    }

    if (entry.showVolumeProfile) drawVolumeProfile(ctx, g, entry);

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

    placeTooltip(entry.tooltip, canvas.parentElement, x, y);
}
