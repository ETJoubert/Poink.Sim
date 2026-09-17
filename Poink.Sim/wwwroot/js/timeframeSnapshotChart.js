// Minimal dependency-free canvas renderer for TimeframeSnapshotChart.razor -- draws one bar per
// selected timeframe as an OHLC/Quartile box, labelled underneath by its timeframe instead of a
// timestamp, since each bar summarizes its own overlapping trailing window rather than a
// sequential time series (see candlestickChart.js for the time-series version).
const PADDING = { top: 22, right: 64, bottom: 20, left: 8 };
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
        entryPrice: options.entryPrice ?? null,
        positionIsLong: options.positionIsLong ?? null,
        stopLossPrice: options.stopLossPrice ?? null,
        takeProfitPrice: options.takeProfitPrice ?? null,
        draftEntryPrice: options.draftEntryPrice ?? null,
        draftIsLong: options.draftIsLong ?? null,
        draftStopLossPrice: options.draftStopLossPrice ?? null,
        draftTakeProfitPrice: options.draftTakeProfitPrice ?? null,
        highlightWidth: existing?.highlightWidth ?? DEFAULT_HIGHLIGHT_WIDTH,
        hover: existing?.hover ?? null,
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

// Layout shared by draw() and hover hit-testing, so the crosshair always lines up with what's drawn.
function geometryFor(canvas, entry) {
    const width = canvas.clientWidth || 600;
    const height = canvas.clientHeight || 240;
    const highlightWidth = hasMargin(entry) ? entry.highlightWidth : 0;
    const chartWidth = width - PADDING.left - PADDING.right - highlightWidth;
    const chartHeight = height - PADDING.top - PADDING.bottom;
    const plotRight = PADDING.left + chartWidth;

    const prices = entry.bars.flatMap(b => [b.low, b.high]);
    [
        entry.latestPrice,
        entry.entryPrice,
        entry.stopLossPrice,
        entry.takeProfitPrice,
        entry.draftEntryPrice,
        entry.draftStopLossPrice,
        entry.draftTakeProfitPrice,
    ].forEach(p => {
        if (p !== null && p !== undefined) prices.push(p);
    });
    const minPrice = prices.length > 0 ? Math.min(...prices) : 0;
    const maxPrice = prices.length > 0 ? Math.max(...prices) : 1;
    const priceRange = (maxPrice - minPrice) || 1;
    const slotWidth = entry.bars.length > 0 ? chartWidth / entry.bars.length : chartWidth;

    return {
        width,
        height,
        chartWidth,
        chartHeight,
        plotRight,
        highlightWidth,
        minPrice,
        maxPrice,
        priceRange,
        slotWidth,
        yFor: price => PADDING.top + chartHeight - ((price - minPrice) / priceRange) * chartHeight,
        priceFor: y => minPrice + (1 - (y - PADDING.top) / chartHeight) * priceRange,
        xFor: index => PADDING.left + index * slotWidth + slotWidth / 2,
    };
}

// Drag-to-resize the highlight margin (same pointer-capture pattern as the project's chart
// dividers, hit-tested against a canvas coordinate instead of a dedicated DOM element) and,
// when not dragging, track the hovered bar/price for the crosshair readout.
function bindResize(canvas) {
    if (resizeBound.has(canvas)) return;
    resizeBound.add(canvas);

    canvas.addEventListener('pointermove', e => {
        const entry = state.get(canvas);
        if (!entry) return;
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        const drag = resizeDrag.get(canvas);

        if (drag?.dragging) {
            const maxWidth = Math.max(MIN_HIGHLIGHT_WIDTH, rect.width * 0.5);
            const delta = drag.startX - x;
            entry.highlightWidth = Math.min(maxWidth, Math.max(MIN_HIGHLIGHT_WIDTH, drag.startWidth + delta));
            draw(canvas);
            return;
        }

        const boundaryX = highlightBoundaryX(canvas);
        canvas.style.cursor = boundaryX !== null && Math.abs(x - boundaryX) <= RESIZE_HIT_RADIUS ? 'col-resize' : 'default';

        const geo = geometryFor(canvas, entry);
        const inPlot = entry.bars.length > 0 && x >= PADDING.left && x <= geo.plotRight && y >= PADDING.top && y <= PADDING.top + geo.chartHeight;
        entry.hover = inPlot ? { index: Math.min(entry.bars.length - 1, Math.max(0, Math.floor((x - PADDING.left) / geo.slotWidth))), y } : null;
        draw(canvas);
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

    canvas.addEventListener('pointerleave', () => {
        const entry = state.get(canvas);
        if (entry?.hover) {
            entry.hover = null;
            draw(canvas);
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
    const entryPrice = entry?.entryPrice ?? null;
    const positionIsLong = entry?.positionIsLong ?? null;
    const stopLossPrice = entry?.stopLossPrice ?? null;
    const takeProfitPrice = entry?.takeProfitPrice ?? null;
    const draftEntryPrice = entry?.draftEntryPrice ?? null;
    const draftIsLong = entry?.draftIsLong ?? null;
    const draftStopLossPrice = entry?.draftStopLossPrice ?? null;
    const draftTakeProfitPrice = entry?.draftTakeProfitPrice ?? null;
    if (bars.length === 0) return;

    const bullColor = cssVar('--color-success', '#00ff9d');
    const bearColor = cssVar('--color-error', '#ff5f56');
    const latestColor = cssVar('--color-highlight', '#ffd166');
    const coverageColor = cssVar('--color-secondary', '#00d4ff');
    const volumeColor = cssVar('--color-primary', '#00ff9d');
    // Same badge colour TradeChartPanel's own draft-order preview lines use (see marketChart.js's
    // groupColors.draft), so a draft reads the same way across every chart it can appear on.
    const draftColor = cssVar('--color-badge-3', '#c792ea');
    const gridColor = 'rgba(255,255,255,0.08)';
    const labelColor = 'rgba(255,255,255,0.5)';

    const { chartWidth, chartHeight, plotRight, highlightWidth, minPrice, maxPrice, slotWidth, yFor, priceFor, xFor } =
        geometryFor(canvas, entry);
    const bodyWidth = Math.max(1, slotWidth * 0.5);

    // Position zones: translucent fill from Entry to SL (red) and Entry to TP (green), same idea
    // as XGrid's draft-order zone shading -- drawn first so everything else layers on top of it.
    if (entryPrice !== null) {
        const entryY = yFor(entryPrice);
        const zoneWidth = width - PADDING.left - PADDING.right;
        ctx.globalAlpha = 0.12;
        if (stopLossPrice !== null) {
            const slY = yFor(stopLossPrice);
            ctx.fillStyle = bearColor;
            ctx.fillRect(PADDING.left, Math.min(entryY, slY), zoneWidth, Math.abs(slY - entryY));
        }
        if (takeProfitPrice !== null) {
            const tpY = yFor(takeProfitPrice);
            ctx.fillStyle = bullColor;
            ctx.fillRect(PADDING.left, Math.min(entryY, tpY), zoneWidth, Math.abs(tpY - entryY));
        }
        ctx.globalAlpha = 1;
    }
    if (draftEntryPrice !== null) {
        const draftEntryY = yFor(draftEntryPrice);
        const zoneWidth = width - PADDING.left - PADDING.right;
        ctx.globalAlpha = 0.12;
        ctx.fillStyle = draftColor;
        if (draftStopLossPrice !== null) {
            const slY = yFor(draftStopLossPrice);
            ctx.fillRect(PADDING.left, Math.min(draftEntryY, slY), zoneWidth, Math.abs(slY - draftEntryY));
        }
        if (draftTakeProfitPrice !== null) {
            const tpY = yFor(draftTakeProfitPrice);
            ctx.fillRect(PADDING.left, Math.min(draftEntryY, tpY), zoneWidth, Math.abs(tpY - draftEntryY));
        }
        ctx.globalAlpha = 1;
    }

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

    // Position levels: Entry solid/neutral, SL/TP dashed red/green -- same semantics as XGrid and
    // MarketChart use elsewhere, drawn as a full-width line plus a canvas-text price label.
    const drawLevelLine = (price, color, label, dashed) => {
        if (price === null) return;
        const y = yFor(price);
        ctx.save();
        ctx.strokeStyle = color;
        if (dashed) ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(PADDING.left, y);
        ctx.lineTo(width - PADDING.right, y);
        ctx.stroke();
        ctx.restore();

        ctx.font = 'bold 11px monospace';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = color;
        ctx.textAlign = 'left';
        ctx.fillText(`${label} ${price.toFixed(5)}`, width - PADDING.right + 4, y);
    };
    const entryLabel = positionIsLong === true ? 'LONG' : positionIsLong === false ? 'SHORT' : 'ENTRY';
    drawLevelLine(entryPrice, 'rgba(220,220,220,0.9)', entryLabel, false);
    drawLevelLine(stopLossPrice, bearColor, 'SL', true);
    drawLevelLine(takeProfitPrice, bullColor, 'TP', true);

    // Draft preview levels: same entry/SL/TP semantics as the position above, drawn in the
    // project's shared draft colour so this chart reads the same as XGrid/MarketChart's own previews.
    const draftEntryLabel = draftIsLong === true ? 'DRAFT LONG' : draftIsLong === false ? 'DRAFT SHORT' : 'DRAFT';
    drawLevelLine(draftEntryPrice, draftColor, draftEntryLabel, true);
    drawLevelLine(draftStopLossPrice, draftColor, 'DRAFT SL', true);
    drawLevelLine(draftTakeProfitPrice, draftColor, 'DRAFT TP', true);

    // Crosshair: vertical line snaps to the hovered bar's center (bars are discrete columns, not a
    // continuous series), horizontal line follows the cursor exactly for a precise price readout.
    if (entry.hover) {
        const bar = bars[entry.hover.index];
        const x = xFor(entry.hover.index);
        const cursorY = Math.min(Math.max(entry.hover.y, PADDING.top), PADDING.top + chartHeight);
        const cursorPrice = priceFor(cursorY);

        ctx.save();
        ctx.strokeStyle = 'rgba(255,255,255,0.45)';
        ctx.setLineDash([2, 2]);
        ctx.beginPath();
        ctx.moveTo(x, PADDING.top);
        ctx.lineTo(x, PADDING.top + chartHeight);
        ctx.moveTo(PADDING.left, cursorY);
        ctx.lineTo(width - PADDING.right, cursorY);
        ctx.stroke();
        ctx.restore();

        const priceText = cursorPrice.toFixed(5);
        ctx.font = 'bold 11px monospace';
        ctx.textBaseline = 'middle';
        const tagWidth = ctx.measureText(priceText).width + 8;
        ctx.fillStyle = 'rgba(0,0,0,0.75)';
        ctx.fillRect(width - PADDING.right + 2, cursorY - 8, tagWidth, 16);
        ctx.fillStyle = '#fff';
        ctx.textAlign = 'left';
        ctx.fillText(priceText, width - PADDING.right + 6, cursorY);

        ctx.font = '11px monospace';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = labelColor;
        let readout =
            `${bar.label}   O ${bar.open.toFixed(5)}   H ${bar.high.toFixed(5)}   ` +
            `L ${bar.low.toFixed(5)}   C ${bar.close.toFixed(5)}   Vol ${bar.volume.toLocaleString()}`;

        if (showHighLowLines) {
            const hlCount = bars.filter(b => cursorPrice >= b.low && cursorPrice <= b.high).length;
            readout += `   HL ${hlCount}/${bars.length}`;
        }

        if (showVolumeProfile && volumeProfile.length > 0) {
            const bucket = volumeProfile.find(b => cursorPrice >= b.low && cursorPrice <= b.high);
            readout += `   VP ${(bucket?.count ?? 0).toLocaleString()}`;
        }

        ctx.fillText(readout, PADDING.left, PADDING.top / 2);
    }
}
