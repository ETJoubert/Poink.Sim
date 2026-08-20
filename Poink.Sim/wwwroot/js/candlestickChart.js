// Minimal dependency-free OHLC candlestick renderer for CandlestickChart.razor -- no pan/
// zoom/crosshair/volume, just High-Low wicks + Open-Close bodies over a fixed-padding
// canvas (see marketChart.js for the full lightweight-charts-backed view).
const PADDING = { top: 10, right: 64, bottom: 24, left: 8 };

const state = new Map();

export function render(canvas, bars, referenceBarCount) {
    state.set(canvas, { bars, referenceBarCount: referenceBarCount ?? 0 });
    draw(canvas);
}

export function dispose(canvas) {
    state.delete(canvas);
}

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

function formatTime(unixSeconds) {
    return new Date(unixSeconds * 1000).toLocaleString(undefined, {
        month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
    });
}

function draw(canvas) {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const dpr = window.devicePixelRatio || 1;
    const width = canvas.clientWidth || 600;
    const height = canvas.clientHeight || 320;
    canvas.width = width * dpr;
    canvas.height = height * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    const entry = state.get(canvas);
    const bars = entry?.bars ?? [];
    const referenceBarCount = entry?.referenceBarCount ?? 0;
    if (bars.length === 0) return;

    const bullColor = cssVar('--color-success', '#00ff9d');
    const bearColor = cssVar('--color-error', '#ff5f56');
    const refColor = cssVar('--color-error', '#ff5f56');
    const gridColor = 'rgba(255,255,255,0.08)';
    const labelColor = 'rgba(255,255,255,0.5)';

    const chartWidth = width - PADDING.left - PADDING.right;
    const chartHeight = height - PADDING.top - PADDING.bottom;

    const minPrice = Math.min(...bars.map(b => b.low));
    const maxPrice = Math.max(...bars.map(b => b.high));
    const priceRange = (maxPrice - minPrice) || 1;

    const slotWidth = chartWidth / bars.length;
    const bodyWidth = Math.max(1, slotWidth * 0.6);

    const yFor = price => PADDING.top + chartHeight - ((price - minPrice) / priceRange) * chartHeight;
    const xFor = index => PADDING.left + index * slotWidth + slotWidth / 2;

    // Gridlines + price labels
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

    // T-Range Reference Chart's lookback window -- the first referenceBarCount bars
    // shaded, with a dashed boundary, so it reads as its own area rather than "more
    // candles". Drawn before the candles so it sits behind them.
    if (referenceBarCount > 0 && referenceBarCount < bars.length) {
        const boundaryX = xFor(referenceBarCount - 1) + slotWidth / 2;
        ctx.save();
        ctx.globalAlpha = 0.12;
        ctx.fillStyle = refColor;
        ctx.fillRect(PADDING.left, PADDING.top, boundaryX - PADDING.left, chartHeight);
        ctx.restore();

        ctx.save();
        ctx.strokeStyle = refColor;
        ctx.setLineDash([3, 3]);
        ctx.beginPath();
        ctx.moveTo(boundaryX, PADDING.top);
        ctx.lineTo(boundaryX, PADDING.top + chartHeight);
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.restore();
    }

    // Candles
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
    });

    // First/last time labels
    ctx.fillStyle = labelColor;
    ctx.textBaseline = 'top';
    ctx.textAlign = 'left';
    ctx.fillText(formatTime(bars[0].time), PADDING.left, height - PADDING.bottom + 6);
    ctx.textAlign = 'right';
    ctx.fillText(formatTime(bars[bars.length - 1].time), width - PADDING.right, height - PADDING.bottom + 6);
}
