// Minimal dependency-free canvas renderer for TimeframeSnapshotChart.razor -- draws one bar per
// selected timeframe as an OHLC/Quartile box, labelled underneath by its timeframe instead of a
// timestamp, since each bar summarizes its own overlapping trailing window rather than a
// sequential time series (see candlestickChart.js for the time-series version).
const PADDING = { top: 10, right: 64, bottom: 20, left: 8 };
const DEFAULT_HIGHLIGHT_WIDTH = 48;
const MIN_HIGHLIGHT_WIDTH = 8;
const RESIZE_HIT_RADIUS = 4;

const state = new Map();
const resizeDrag = new WeakMap();
const resizeBound = new WeakSet();

export function render(canvas, options) {
    const existing = state.get(canvas);
    state.set(canvas, {
        bars: Array.from(options.bars ?? []),
        latestPrice: options.latestPrice ?? null,
        showHighLowLines: !!options.showHighLowLines,
        showVolumeProfile: !!options.showVolumeProfile,
        volumeProfile: Array.from(options.volumeProfile ?? []),
        highlightWidth: existing?.highlightWidth ?? DEFAULT_HIGHLIGHT_WIDTH,
    });
    bindResize(canvas);
    draw(canvas);
}

export function dispose(canvas) {
    state.delete(canvas);
}

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

function hasMargin(entry) {
    return !!entry && entry.bars.length > 0 && (entry.showHighLowLines || entry.showVolumeProfile);
}

// The highlight margin's left edge, in canvas CSS pixels -- null while there's no margin to drag.
function highlightBoundaryX(canvas) {
    const entry = state.get(canvas);
    if (!hasMargin(entry)) return null;
    return (canvas.clientWidth || 600) - PADDING.right - entry.highlightWidth;
}

// Drag-to-resize the highlight margin -- same pointer-capture pattern as the project's chart
// dividers, but hit-tested against a canvas coordinate instead of a dedicated DOM element.
function bindResize(canvas) {
    if (resizeBound.has(canvas)) return;
    resizeBound.add(canvas);

    canvas.addEventListener('pointermove', e => {
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const drag = resizeDrag.get(canvas);

        if (drag?.dragging) {
            const entry = state.get(canvas);
            const maxWidth = Math.max(MIN_HIGHLIGHT_WIDTH, rect.width * 0.5);
            const delta = drag.startX - x;
            entry.highlightWidth = Math.min(maxWidth, Math.max(MIN_HIGHLIGHT_WIDTH, drag.startWidth + delta));
            draw(canvas);
            return;
        }

        const boundaryX = highlightBoundaryX(canvas);
        canvas.style.cursor = boundaryX !== null && Math.abs(x - boundaryX) <= RESIZE_HIT_RADIUS ? 'col-resize' : 'default';
    });

    canvas.addEventListener('pointerdown', e => {
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const boundaryX = highlightBoundaryX(canvas);
        if (boundaryX === null || Math.abs(x - boundaryX) > RESIZE_HIT_RADIUS) return;

        canvas.setPointerCapture(e.pointerId);
        resizeDrag.set(canvas, { dragging: true, startX: x, startWidth: state.get(canvas).highlightWidth });
    });

    canvas.addEventListener('pointerup', e => {
        if (resizeDrag.get(canvas)?.dragging) {
            canvas.releasePointerCapture(e.pointerId);
            resizeDrag.set(canvas, { dragging: false });
        }
    });
}

function draw(canvas) {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const dpr = window.devicePixelRatio || 1;
    const width = canvas.clientWidth || 600;
    const height = canvas.clientHeight || 240;
    canvas.width = width * dpr;
    canvas.height = height * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    const entry = state.get(canvas);
    const bars = entry?.bars ?? [];
    const latestPrice = entry?.latestPrice ?? null;
    const showHighLowLines = entry?.showHighLowLines ?? false;
    const showVolumeProfile = entry?.showVolumeProfile ?? false;
    const volumeProfile = entry?.volumeProfile ?? [];
    if (bars.length === 0) return;

    const bullColor = cssVar('--color-success', '#00ff9d');
    const bearColor = cssVar('--color-error', '#ff5f56');
    const latestColor = cssVar('--color-highlight', '#ffd166');
    const coverageColor = cssVar('--color-secondary', '#00d4ff');
    const volumeColor = cssVar('--color-primary', '#00ff9d');
    const gridColor = 'rgba(255,255,255,0.08)';
    const labelColor = 'rgba(255,255,255,0.5)';

    const highlightWidth = hasMargin(entry) ? entry.highlightWidth : 0;
    const chartWidth = width - PADDING.left - PADDING.right - highlightWidth;
    const chartHeight = height - PADDING.top - PADDING.bottom;
    const plotRight = PADDING.left + chartWidth;

    const prices = bars.flatMap(b => [b.low, b.high]);
    if (latestPrice !== null) prices.push(latestPrice);
    const minPrice = Math.min(...prices);
    const maxPrice = Math.max(...prices);
    const priceRange = (maxPrice - minPrice) || 1;

    const slotWidth = chartWidth / bars.length;
    const bodyWidth = Math.max(1, slotWidth * 0.5);

    const yFor = price => PADDING.top + chartHeight - ((price - minPrice) / priceRange) * chartHeight;
    const priceFor = y => minPrice + (1 - (y - PADDING.top) / chartHeight) * priceRange;
    const xFor = index => PADDING.left + index * slotWidth + slotWidth / 2;

    if (highlightWidth > 0) {
        // Coverage histogram: each row's bar length is how many displayed bars' High-Low range
        // covers that price, so it reads as "where the timeframes' ranges pile up".
        if (showHighLowLines) {
            ctx.fillStyle = coverageColor;
            ctx.globalAlpha = 0.55;
            for (let y = PADDING.top; y < PADDING.top + chartHeight; y++) {
                const price = priceFor(y + 0.5);
                const coverage = bars.filter(b => price >= b.low && price <= b.high).length / bars.length;
                if (coverage <= 0) continue;
                ctx.fillRect(plotRight, y, coverage * highlightWidth, 1);
            }
            ctx.globalAlpha = 1;
        }

        // Real tick-volume profile: one bar per price bucket, length proportional to that
        // bucket's share of the busiest bucket -- same count/maxCount convention as the
        // project's other volume profile charts.
        if (showVolumeProfile && volumeProfile.length > 0) {
            const maxCount = Math.max(...volumeProfile.map(b => b.count));
            ctx.fillStyle = volumeColor;
            ctx.globalAlpha = 0.55;
            volumeProfile.forEach(bucket => {
                const top = yFor(bucket.high);
                const bottom = yFor(bucket.low);
                const barWidth = maxCount > 0 ? (bucket.count / maxCount) * highlightWidth : 0;
                ctx.fillRect(plotRight, top, barWidth, Math.max(1, bottom - top));
            });
            ctx.globalAlpha = 1;
        }

        ctx.strokeStyle = gridColor;
        ctx.beginPath();
        ctx.moveTo(plotRight, PADDING.top);
        ctx.lineTo(plotRight, PADDING.top + chartHeight);
        ctx.stroke();
    }

    ctx.font = '11px monospace';
    ctx.textBaseline = 'middle';
    [minPrice, (minPrice + maxPrice) / 2, maxPrice].forEach(price => {
        const y = yFor(price);
        ctx.strokeStyle = gridColor;
        ctx.beginPath();
        ctx.moveTo(PADDING.left, y);
        ctx.lineTo(width - PADDING.right, y);
        ctx.stroke();

        ctx.fillStyle = labelColor;
        ctx.textAlign = 'left';
        ctx.fillText(price.toFixed(5), width - PADDING.right + 4, y);
    });

    if (showHighLowLines) {
        ctx.save();
        ctx.setLineDash([3, 3]);
        ctx.globalAlpha = 0.35;
        bars.forEach(bar => {
            [
                [bar.high, bullColor],
                [bar.low, bearColor],
            ].forEach(([price, color]) => {
                const y = yFor(price);
                ctx.strokeStyle = color;
                ctx.beginPath();
                ctx.moveTo(PADDING.left, y);
                ctx.lineTo(width - PADDING.right, y);
                ctx.stroke();
            });
        });
        ctx.restore();
    }

    bars.forEach((bar, i) => {
        const x = xFor(i);
        const isBull = bar.close >= bar.open;
        ctx.strokeStyle = ctx.fillStyle = isBull ? bullColor : bearColor;

        ctx.beginPath();
        ctx.moveTo(x, yFor(bar.high));
        ctx.lineTo(x, yFor(bar.low));
        ctx.stroke();

        const openY = yFor(bar.open);
        const closeY = yFor(bar.close);
        const top = Math.min(openY, closeY);
        const bodyHeight = Math.max(1, Math.abs(closeY - openY));
        ctx.fillRect(x - bodyWidth / 2, top, bodyWidth, bodyHeight);

        ctx.fillStyle = labelColor;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'top';
        ctx.fillText(bar.label, x, height - PADDING.bottom + 4);
    });

    if (latestPrice !== null) {
        const y = yFor(latestPrice);
        ctx.save();
        ctx.strokeStyle = latestColor;
        ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(PADDING.left, y);
        ctx.lineTo(width - PADDING.right, y);
        ctx.stroke();
        ctx.restore();

        ctx.font = 'bold 11px monospace';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = latestColor;
        ctx.textAlign = 'left';
        ctx.fillText(latestPrice.toFixed(5), width - PADDING.right + 4, y);
    }
}
