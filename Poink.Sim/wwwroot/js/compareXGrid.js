// Canvas renderer for CompareXGrid.razor.cs: % rows by time columns, each cell coloured from every symbol's
// share of its own busiest cell -- 'dominant' takes the highest share's colour, 'difference' shades towards
// whichever of the first symbol and the chosen one has the higher share.
import { cssColor, withAlpha } from './chartColors.js';

const PADDING = { top: 6, right: 54, bottom: 20, left: 4 };
const PROFILE_GAP = 6;
const PROFILE_OPACITY = 0.45;
const MARKED_DIM = 0.3;
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
    entry.symbols = Array.from(options.symbols).map(s => {
        const rowTotals = rowSums(s.counts, options.rowCount, options.colCount);
        return {
            label: s.label,
            color: cssColor(canvas, s.colorVar),
            counts: s.counts,
            max: s.max,
            rowTotals,
            maxRowTotal: Math.max(0, ...rowTotals),
            topThreshold: cutoff(s.counts, options.topCellsPercent, true),
            lowThreshold: cutoff(s.counts, options.lowCellsPercent, false),
        };
    });
    draw(canvas);
}

function rowSums(counts, rowCount, colCount) {
    const totals = new Array(rowCount).fill(0);
    for (let r = 0; r < rowCount; r++)
        for (let c = 0; c < colCount; c++) totals[r] += counts[r * colCount + c];
    return totals;
}

// The count at the edge of a symbol's busiest (or quietest) percent of non-empty cells; null when off or empty.
// Empty cells are left out, otherwise every untraded cell would count as one of the quietest.
function cutoff(counts, percent, busiest) {
    if (!percent) return null;
    const nonEmpty = Array.from(counts).filter(c => c > 0).sort((a, b) => (busiest ? b - a : a - b));
    if (nonEmpty.length === 0) return null;
    return nonEmpty[Math.max(0, Math.ceil((nonEmpty.length * percent) / 100) - 1)];
}

function isLowCell(symbol, index) {
    return symbol.lowThreshold !== null && symbol.counts[index] > 0 && symbol.counts[index] <= symbol.lowThreshold;
}

function hasMarkers(o) {
    return !!(o.topCellsPercent || o.lowCellsPercent);
}

function shownIndices(entry) {
    const o = entry.options;
    return o.mode === 'difference' && o.differenceIndex > 0 ? [0, o.differenceIndex] : entry.symbols.map((_, i) => i);
}

function isTopCell(symbol, index) {
    return symbol.topThreshold !== null && symbol.counts[index] > 0 && symbol.counts[index] >= symbol.topThreshold;
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
    const available = Math.max(1, width - PADDING.left - PADDING.right);
    const profileWidth = o.showProfile ? Math.min(120, Math.max(40, available * 0.2)) : 0;
    const plotWidth = Math.max(1, available - (o.showProfile ? profileWidth + PROFILE_GAP : 0));
    const plotHeight = Math.max(1, height - PADDING.top - PADDING.bottom);
    const profileLeft = plotLeft + plotWidth + PROFILE_GAP;
    return {
        width, height, plotLeft, plotTop, plotWidth, plotHeight, profileLeft, profileWidth,
        axisX: (o.showProfile ? profileLeft + profileWidth : plotLeft + plotWidth) + 6,
        cellWidth: plotWidth / o.colCount,
        cellHeight: plotHeight / o.rowCount,
    };
}

function shares(entry, index) {
    return entry.symbols.map(s => (s.max > 0 ? s.counts[index] / s.max : 0));
}

// Square root lifts sparse cells into view without letting the busiest ones saturate everything else;
// dim fades the fills while top/low outlines are on so the outlines read clearly.
function cellFill(entry, index, dim) {
    const o = entry.options;
    const s = shares(entry, index);
    if (o.mode === 'difference' && o.differenceIndex > 0) {
        const diff = s[0] - s[o.differenceIndex];
        if (diff === 0) return null;
        const color = diff > 0 ? entry.symbols[0].color : entry.symbols[o.differenceIndex].color;
        return withAlpha(color, (0.05 + 0.9 * Math.sqrt(Math.abs(diff))) * dim);
    }

    let best = -1;
    for (let i = 0; i < s.length; i++) if (s[i] > 0 && (best < 0 || s[i] > s[best])) best = i;
    return best < 0 ? null : withAlpha(entry.symbols[best].color, (0.05 + 0.9 * Math.sqrt(s[best])) * dim);
}

function percentDecimals(rowPercent) {
    return rowPercent < 0.05 ? 3 : 2;
}

function formatTime(unix, spanSeconds) {
    const iso = new Date(unix * 1000).toISOString();
    return spanSeconds > 86400 ? `${iso.slice(5, 10)} ${iso.slice(11, 16)}` : iso.slice(11, 16);
}

// Each shown symbol's outline is inset one step further, so several symbols' marked cells stay visible in one cell;
// top cells are solid and low cells dashed.
function drawMarkedCells(ctx, g, entry, shown) {
    const o = entry.options;
    const inset = Math.max(1, Math.min(2, Math.min(g.cellWidth, g.cellHeight) / (2 * (shown.length + 1))));
    ctx.save();
    ctx.lineWidth = 1;
    for (let r = 0; r < o.rowCount; r++) {
        for (let c = 0; c < o.colCount; c++) {
            const index = r * o.colCount + c;
            shown.forEach((symbolIndex, order) => {
                const symbol = entry.symbols[symbolIndex];
                const top = isTopCell(symbol, index);
                const low = isLowCell(symbol, index);
                if (!top && !low) return;
                const pad = inset * order + 0.5;
                ctx.strokeStyle = symbol.color;
                ctx.setLineDash(top ? [] : [2, 2]);
                ctx.strokeRect(
                    g.plotLeft + c * g.cellWidth + pad,
                    g.plotTop + r * g.cellHeight + pad,
                    Math.max(1, g.cellWidth - 2 * pad),
                    Math.max(1, g.cellHeight - 2 * pad),
                );
            });
        }
    }
    ctx.restore();
}

function drawProfile(ctx, g, entry, shown, dim) {
    const o = entry.options;
    ctx.strokeStyle = 'rgba(255,255,255,0.12)';
    ctx.beginPath();
    ctx.moveTo(Math.round(g.profileLeft) + 0.5, g.plotTop);
    ctx.lineTo(Math.round(g.profileLeft) + 0.5, g.plotTop + g.plotHeight);
    ctx.stroke();

    for (const symbolIndex of shown) {
        const symbol = entry.symbols[symbolIndex];
        if (symbol.maxRowTotal <= 0) continue;
        ctx.fillStyle = withAlpha(symbol.color, PROFILE_OPACITY * dim);
        for (let r = 0; r < o.rowCount; r++) {
            const total = symbol.rowTotals[r];
            if (total <= 0) continue;
            ctx.fillRect(
                g.profileLeft + 1,
                g.plotTop + r * g.cellHeight,
                Math.max(1, (total / symbol.maxRowTotal) * (g.profileWidth - 1)),
                Math.max(1, g.cellHeight - (g.cellHeight > 3 ? 1 : 0)),
            );
        }
    }
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

    const dim = hasMarkers(o) ? MARKED_DIM : 1;
    for (let r = 0; r < o.rowCount; r++) {
        for (let c = 0; c < o.colCount; c++) {
            const fill = cellFill(entry, r * o.colCount + c, dim);
            if (!fill) continue;
            ctx.fillStyle = fill;
            ctx.fillRect(g.plotLeft + c * g.cellWidth, g.plotTop + r * g.cellHeight, Math.ceil(g.cellWidth), Math.ceil(g.cellHeight));
        }
    }

    const shown = shownIndices(entry);
    if (hasMarkers(o)) drawMarkedCells(ctx, g, entry, shown);
    if (o.showProfile) drawProfile(ctx, g, entry, shown, dim);

    const zeroRow = o.topPercent / o.rowPercent;
    if (zeroRow >= 0 && zeroRow <= o.rowCount) {
        const y = Math.round(g.plotTop + zeroRow * g.cellHeight) + 0.5;
        ctx.strokeStyle = 'rgba(255,255,255,0.45)';
        ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(g.plotLeft, y);
        ctx.lineTo(o.showProfile ? g.profileLeft + g.profileWidth : g.plotLeft + g.plotWidth, y);
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
        ctx.fillText(`${(Math.abs(value) < 1e-9 ? 0 : value).toFixed(decimals)}%`, g.axisX, g.plotTop + r * g.cellHeight);
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
        const profileRow = entry.hover.col === null;
        ctx.strokeStyle = 'rgba(255,255,255,0.8)';
        ctx.lineWidth = 1;
        ctx.strokeRect(
            (profileRow ? g.profileLeft : g.plotLeft + entry.hover.col * g.cellWidth) + 0.5,
            g.plotTop + entry.hover.row * g.cellHeight + 0.5,
            Math.max(1, (profileRow ? g.profileWidth : g.cellWidth) - 1),
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
    const row = Math.floor((y - g.plotTop) / g.cellHeight);
    const col = Math.floor((x - g.plotLeft) / g.cellWidth);
    const rowInside = row >= 0 && row < o.rowCount;
    const inProfile = o.showProfile && rowInside && x >= g.profileLeft && x <= g.profileLeft + g.profileWidth;
    const hover = inProfile ? { row, col: null } : rowInside && col >= 0 && col < o.colCount ? { row, col } : null;

    if (hover?.row !== entry.hover?.row || hover?.col !== entry.hover?.col) {
        entry.hover = hover;
        draw(canvas);
    }
    if (!hover) {
        entry.tooltip.style.display = 'none';
        return;
    }

    const decimals = percentDecimals(o.rowPercent);
    const high = o.topPercent - row * o.rowPercent;
    const band = `<div>${(high - o.rowPercent).toFixed(decimals)}% to ${high.toFixed(decimals)}%</div>`;
    if (hover.col === null) {
        const rows = shownIndices(entry).map(i => {
            const symbol = entry.symbols[i];
            const share = symbol.maxRowTotal > 0 ? symbol.rowTotals[row] / symbol.maxRowTotal : 0;
            return `<div style="color:${symbol.color}">${symbol.label} ${symbol.rowTotals[row].toLocaleString()} ticks (${Math.round(share * 100)}% of its busiest row)</div>`;
        });
        entry.tooltip.innerHTML = `${band}<div>whole window</div>${rows.join('')}`;
    } else {
        const index = row * o.colCount + col;
        const span = o.end - o.columnStarts[0];
        const colEnd = col + 1 < o.colCount ? o.columnStarts[col + 1] : o.end;
        const s = shares(entry, index);
        const rows = entry.symbols.map((symbol, i) => {
            const top = isTopCell(symbol, index) ? ` -- top ${o.topCellsPercent}%` : '';
            const low = isLowCell(symbol, index) ? ` -- lowest ${o.lowCellsPercent}%` : '';
            return `<div style="color:${symbol.color}">${symbol.label} ${symbol.counts[index].toLocaleString()} ticks (${Math.round(s[i] * 100)}% of its busiest)${top}${low}</div>`;
        });
        entry.tooltip.innerHTML =
            `${band}<div>${formatTime(o.columnStarts[col], span)} - ${formatTime(colEnd, span)}</div>${rows.join('')}`;
    }
    entry.tooltip.style.display = 'block';

    const container = canvas.parentElement;
    const left = Math.min(x + 12, (container?.clientWidth ?? g.width) - entry.tooltip.offsetWidth - 8);
    entry.tooltip.style.left = `${Math.max(left, 8)}px`;
    entry.tooltip.style.top = `${Math.max(y - 12, 8)}px`;
}
