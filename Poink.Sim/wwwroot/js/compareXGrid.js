// Canvas renderer for CompareXGrid.razor.cs: % rows by time columns, each cell coloured from every symbol's
// share of its own busiest cell -- 'dominant' takes the highest share's colour, 'difference' shades towards
// whichever of the first symbol and the chosen one has the higher share.
import { cssColor, withAlpha } from './chartColors.js';

const PADDING = { top: 6, right: 54, bottom: 20, left: 4 };
const TEXT_COLOR = '#c9c9c9';
const FONT = '10px monospace';
const MIN_LABEL_SPACING = 18;

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

    entry.options = options;
    entry.symbols = Array.from(options.symbols).map(s => ({
        label: s.label,
        color: cssColor(canvas, s.colorVar),
        counts: s.counts,
        max: s.max,
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

function geometry(canvas, o) {
    const width = canvas.clientWidth || 400;
    const height = canvas.clientHeight || 240;
    const plotLeft = PADDING.left;
    const plotTop = PADDING.top;
    const plotWidth = Math.max(1, width - PADDING.left - PADDING.right);
    const plotHeight = Math.max(1, height - PADDING.top - PADDING.bottom);
    return {
        width, height, plotLeft, plotTop, plotWidth, plotHeight,
        cellWidth: plotWidth / o.colCount,
        cellHeight: plotHeight / o.rowCount,
    };
}

function shares(entry, index) {
    return entry.symbols.map(s => (s.max > 0 ? s.counts[index] / s.max : 0));
}

// Square root lifts sparse cells into view without letting the busiest ones saturate everything else.
function cellFill(entry, index) {
    const o = entry.options;
    const s = shares(entry, index);
    if (o.mode === 'difference' && o.differenceIndex > 0) {
        const diff = s[0] - s[o.differenceIndex];
        if (diff === 0) return null;
        const color = diff > 0 ? entry.symbols[0].color : entry.symbols[o.differenceIndex].color;
        return withAlpha(color, 0.05 + 0.9 * Math.sqrt(Math.abs(diff)));
    }

    let best = -1;
    for (let i = 0; i < s.length; i++) if (s[i] > 0 && (best < 0 || s[i] > s[best])) best = i;
    return best < 0 ? null : withAlpha(entry.symbols[best].color, 0.05 + 0.9 * Math.sqrt(s[best]));
}

function percentDecimals(rowPercent) {
    return rowPercent < 0.05 ? 3 : 2;
}

function formatTime(unix, spanSeconds) {
    const iso = new Date(unix * 1000).toISOString();
    return spanSeconds > 86400 ? `${iso.slice(5, 10)} ${iso.slice(11, 16)}` : iso.slice(11, 16);
}

function draw(canvas) {
    const entry = state.get(canvas);
    if (!entry) return;
    const o = entry.options;
    const g = geometry(canvas, o);

    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(g.width * dpr));
    canvas.height = Math.max(1, Math.round(g.height * dpr));
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, g.width, g.height);
    ctx.font = FONT;

    for (let r = 0; r < o.rowCount; r++) {
        for (let c = 0; c < o.colCount; c++) {
            const fill = cellFill(entry, r * o.colCount + c);
            if (!fill) continue;
            ctx.fillStyle = fill;
            ctx.fillRect(g.plotLeft + c * g.cellWidth, g.plotTop + r * g.cellHeight, Math.ceil(g.cellWidth), Math.ceil(g.cellHeight));
        }
    }

    const zeroRow = o.topPercent / o.rowPercent;
    if (zeroRow >= 0 && zeroRow <= o.rowCount) {
        const y = Math.round(g.plotTop + zeroRow * g.cellHeight) + 0.5;
        ctx.strokeStyle = 'rgba(255,255,255,0.45)';
        ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(g.plotLeft, y);
        ctx.lineTo(g.plotLeft + g.plotWidth, y);
        ctx.stroke();
        ctx.setLineDash([]);
    }

    const labelEvery = Math.max(1, Math.ceil(MIN_LABEL_SPACING / g.cellHeight));
    const decimals = percentDecimals(o.rowPercent);
    ctx.fillStyle = TEXT_COLOR;
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    for (let r = 0; r <= o.rowCount; r += labelEvery) {
        const value = o.topPercent - r * o.rowPercent;
        ctx.fillText(`${(Math.abs(value) < 1e-9 ? 0 : value).toFixed(decimals)}%`, g.plotLeft + g.plotWidth + 6, g.plotTop + r * g.cellHeight);
    }

    const span = o.end - o.columnStarts[0];
    ctx.textBaseline = 'top';
    const labelY = g.plotTop + g.plotHeight + 5;
    ctx.textAlign = 'left';
    ctx.fillText(formatTime(o.columnStarts[0], span), g.plotLeft, labelY);
    ctx.textAlign = 'right';
    ctx.fillText(formatTime(o.end, span), g.plotLeft + g.plotWidth, labelY);
    if (o.colCount > 2) {
        const mid = Math.floor(o.colCount / 2);
        ctx.textAlign = 'center';
        ctx.fillText(formatTime(o.columnStarts[mid], span), g.plotLeft + mid * g.cellWidth, labelY);
    }

    if (entry.hover) {
        ctx.strokeStyle = 'rgba(255,255,255,0.8)';
        ctx.strokeRect(
            g.plotLeft + entry.hover.col * g.cellWidth + 0.5,
            g.plotTop + entry.hover.row * g.cellHeight + 0.5,
            Math.max(1, g.cellWidth - 1),
            Math.max(1, g.cellHeight - 1),
        );
    }
}

function onHover(canvas, e) {
    const entry = state.get(canvas);
    if (!entry) return;
    const o = entry.options;
    const g = geometry(canvas, o);
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const col = Math.floor((x - g.plotLeft) / g.cellWidth);
    const row = Math.floor((y - g.plotTop) / g.cellHeight);
    const inside = col >= 0 && col < o.colCount && row >= 0 && row < o.rowCount;
    const hover = inside ? { row, col } : null;

    if (hover?.row !== entry.hover?.row || hover?.col !== entry.hover?.col) {
        entry.hover = hover;
        draw(canvas);
    }
    if (!hover) {
        entry.tooltip.style.display = 'none';
        return;
    }

    const index = row * o.colCount + col;
    const decimals = percentDecimals(o.rowPercent);
    const high = o.topPercent - row * o.rowPercent;
    const low = high - o.rowPercent;
    const span = o.end - o.columnStarts[0];
    const colEnd = col + 1 < o.colCount ? o.columnStarts[col + 1] : o.end;
    const s = shares(entry, index);
    const rows = entry.symbols.map((symbol, i) =>
        `<div style="color:${symbol.color}">${symbol.label} ${symbol.counts[index].toLocaleString()} ticks (${Math.round(s[i] * 100)}% of its busiest)</div>`);
    entry.tooltip.innerHTML =
        `<div>${low.toFixed(decimals)}% to ${high.toFixed(decimals)}%</div>` +
        `<div>${formatTime(o.columnStarts[col], span)} - ${formatTime(colEnd, span)}</div>${rows.join('')}`;
    entry.tooltip.style.display = 'block';

    const container = canvas.parentElement;
    const left = Math.min(x + 12, (container?.clientWidth ?? g.width) - entry.tooltip.offsetWidth - 8);
    entry.tooltip.style.left = `${Math.max(left, 8)}px`;
    entry.tooltip.style.top = `${Math.max(y - 12, 8)}px`;
}
