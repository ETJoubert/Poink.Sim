// Minimal dependency-free canvas renderer for TimeframeSnapshotChart.razor -- draws one bar per
// selected timeframe as an OHLC/Quartile box, labelled underneath by its timeframe instead of a
// timestamp, since each bar summarizes its own overlapping trailing window rather than a
// sequential time series (see candlestickChart.js for the time-series version).
const PADDING = { top: 10, right: 64, bottom: 20, left: 8 };

const state = new Map();

export function render(canvas, bars) {
    state.set(canvas, { bars: Array.from(bars) });
    draw(canvas);
}

export function dispose(canvas) {
    state.delete(canvas);
}

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
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

    const bars = state.get(canvas)?.bars ?? [];
    if (bars.length === 0) return;

    const bullColor = cssVar('--color-success', '#00ff9d');
    const bearColor = cssVar('--color-error', '#ff5f56');
    const gridColor = 'rgba(255,255,255,0.08)';
    const labelColor = 'rgba(255,255,255,0.5)';

    const chartWidth = width - PADDING.left - PADDING.right;
    const chartHeight = height - PADDING.top - PADDING.bottom;

    const minPrice = Math.min(...bars.map(b => b.low));
    const maxPrice = Math.max(...bars.map(b => b.high));
    const priceRange = (maxPrice - minPrice) || 1;

    const slotWidth = chartWidth / bars.length;
    const bodyWidth = Math.max(1, slotWidth * 0.5);

    const yFor = price => PADDING.top + chartHeight - ((price - minPrice) / priceRange) * chartHeight;
    const xFor = index => PADDING.left + index * slotWidth + slotWidth / 2;

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
}
