// Minimal dependency-free canvas renderer for TxFold.razor -- draws one bar per selected
// timeframe as an OHLC/Quartile box, labelled underneath by its timeframe instead of a
// timestamp, since each bar summarizes its own overlapping trailing window rather than a
// sequential time series (see candlestickChart.js for the time-series version).
//
// The right margin is split into two independent columns so an open position's tags never
// compete for space with the plain price axis: a POSITION column (Entry/SL/TP + draft preview --
// just a direction triangle or a 2-letter badge, no price, no "DFT" -- so it's thin) sits right
// against the chart, then the PRICE column (grid ticks + latest price, real price digits) sits
// outermost, flush to the right edge. The position column is a fixed, persistent width -- always
// reserved, even with nothing in it -- so the plot never resizes just because a draft got toggled
// on or off, and sized tight to its 2-letter/triangle content so it doesn't sit mostly empty.
const PADDING = { top: 32, right: 68, bottom: 22, left: 10 };
const CROSSHAIR_TEXT_Y = 10;
const MIN_PRICE_COLUMN = 68;
const MAX_PRICE_COLUMN = 120;
const POSITION_COLUMN_WIDTH = 34;
const COLUMN_GAP = 8;
const AXIS_GAP = 6;
const EDGE_MARGIN = 4;
const PILL_PAD_X = 6;
const PILL_HEIGHT = 16;
const PILL_MIN_GAP = 18;
const TRIANGLE_SIZE = 4;
const TRIANGLE_SLOT = 12;
const DEFAULT_HIGHLIGHT_WIDTH = 48;
const MIN_HIGHLIGHT_WIDTH = 8;
const RESIZE_HIT_RADIUS = 4;
const COVERAGE_BAR_HEIGHT = 4;
const COVERAGE_BAR_GAP = 6;

const state = new Map();
const resizeDrag = new WeakMap();
const resizeBound = new WeakSet();

// Normalize mode redraws every price relative to referencePrice (the largest selected timeframe's
// own midpoint, 0%) scaled by referenceRange (its own half-range, +-100%) -- so that bar's own
// High/Low land exactly on +-100%, and any other bar can still land outside that band. Done once
// here, on the way into state, so the rest of the module never needs to know normalize exists.
function toDisplayPrice(price, normalize, referencePrice, referenceRange) {
    if (price === null || price === undefined) return price;
    return normalize ? ((price - referencePrice) / referenceRange) * 100 : price;
}

export function render(canvas, options) {
    const existing = state.get(canvas);
    const normalize = !!options.normalize && !!options.referenceRange;
    const ref = options.referencePrice;
    const refRange = options.referenceRange;
    const toPrice = p => toDisplayPrice(p, normalize, ref, refRange);
    state.set(canvas, {
        bars: Array.from(options.bars ?? []).map(b => ({
            ...b,
            open: toPrice(b.open),
            high: toPrice(b.high),
            low: toPrice(b.low),
            close: toPrice(b.close),
        })),
        barKind: options.barKind ?? 'Ohlc',
        normalize,
        latestPrice: toPrice(options.latestPrice ?? null),
        showHighLowLines: !!options.showHighLowLines,
        showVolumeProfile: !!options.showVolumeProfile,
        volumeProfile: Array.from(options.volumeProfile ?? []).map(b => ({
            ...b,
            low: toPrice(b.low),
            high: toPrice(b.high),
        })),
        entryPrice: toPrice(options.entryPrice ?? null),
        positionIsLong: options.positionIsLong ?? null,
        stopLossPrice: toPrice(options.stopLossPrice ?? null),
        takeProfitPrice: toPrice(options.takeProfitPrice ?? null),
        draftEntryPrice: toPrice(options.draftEntryPrice ?? null),
        draftIsLong: options.draftIsLong ?? null,
        draftStopLossPrice: toPrice(options.draftStopLossPrice ?? null),
        draftTakeProfitPrice: toPrice(options.draftTakeProfitPrice ?? null),
        highlightWidth: existing?.highlightWidth ?? DEFAULT_HIGHLIGHT_WIDTH,
        hover: existing?.hover ?? null,
        priceColumnWidth: existing?.priceColumnWidth ?? PADDING.right,
    });
    bindResize(canvas);
    draw(canvas);
}

// 5 decimal places reads as a real price; normalized values are a % deviation, so 2 decimals
// plus a suffix reads correctly instead.
function formatPrice(price, normalize) {
    return normalize ? `${price.toFixed(2)}%` : price.toFixed(5);
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

// Total width reserved for the price + position columns together -- the position column is a
// fixed persistent lane, so this only varies with the price column's own dynamic sizing.
function rightMarginFor(entry) {
    const priceColumnWidth = entry.priceColumnWidth ?? PADDING.right;
    return priceColumnWidth + COLUMN_GAP + POSITION_COLUMN_WIDTH;
}

// The highlight margin's left edge, in canvas CSS pixels -- null while there's no margin to drag.
function highlightBoundaryX(canvas) {
    const entry = state.get(canvas);
    if (!hasMargin(entry)) return null;
    return (canvas.clientWidth || 600) - rightMarginFor(entry) - entry.highlightWidth;
}

// Layout shared by draw() and hover hit-testing, so the crosshair always lines up with what's
// drawn. The price column is sized per-frame by draw() so it stays exactly as wide as whichever
// price tags are actually on screen -- see priceColumnWidth. The position column is fixed.
function geometryFor(canvas, entry) {
    const width = canvas.clientWidth || 600;
    const height = canvas.clientHeight || 240;
    const priceColumnWidth = entry.priceColumnWidth ?? PADDING.right;
    const rightMargin = rightMarginFor(entry);
    const highlightWidth = hasMargin(entry) ? entry.highlightWidth : 0;
    const chartWidth = width - PADDING.left - rightMargin - highlightWidth;
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
    let minPrice = prices.length > 0 ? Math.min(...prices) : 0;
    let maxPrice = prices.length > 0 ? Math.max(...prices) : 1;
    if (entry.normalize) {
        const maxAbs = Math.max(Math.abs(minPrice), Math.abs(maxPrice));
        minPrice = -maxAbs;
        maxPrice = maxAbs;
    }
    const priceRange = (maxPrice - minPrice) || 1;
    const slotWidth = entry.bars.length > 0 ? chartWidth / entry.bars.length : chartWidth;

    return {
        width,
        height,
        chartWidth,
        chartHeight,
        plotRight,
        priceColumnWidth,
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

function roundedRectPath(ctx, x, y, w, h, r) {
    if (typeof ctx.roundRect === 'function') {
        ctx.beginPath();
        ctx.roundRect(x, y, w, h, r);
        return;
    }
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
}

function parseColor(value) {
    if (!value) return null;
    const hex = value.match(/^#([0-9a-f]{3}|[0-9a-f]{6})$/i);
    if (hex) {
        const digits = hex[1].length === 3 ? hex[1].split('').map(c => c + c).join('') : hex[1];
        const num = parseInt(digits, 16);
        return [(num >> 16) & 255, (num >> 8) & 255, num & 255];
    }
    const rgb = value.match(/rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i);
    return rgb ? [Number(rgb[1]), Number(rgb[2]), Number(rgb[3])] : null;
}

// Chooses readable text for a solid pill background -- most theme accents are bright enough
// for dark text (e.g. the highlight gold), so a fixed white never reads well on all of them.
function textColorFor(bg) {
    const rgb = parseColor(bg);
    if (!rgb) return '#ffffff';
    const luminance = (0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]) / 255;
    return luminance > 0.6 ? '#0a0a0a' : '#ffffff';
}

// Small filled triangle -- up for long, down for short -- drawn inside a pill just before its
// text, using the same colour as the text so it reads as part of the label, not a separate mark.
function drawDirectionTriangle(ctx, cx, cy, isLong, color) {
    ctx.fillStyle = color;
    ctx.beginPath();
    if (isLong) {
        ctx.moveTo(cx, cy - TRIANGLE_SIZE);
        ctx.lineTo(cx - TRIANGLE_SIZE, cy + TRIANGLE_SIZE);
        ctx.lineTo(cx + TRIANGLE_SIZE, cy + TRIANGLE_SIZE);
    } else {
        ctx.moveTo(cx, cy + TRIANGLE_SIZE);
        ctx.lineTo(cx - TRIANGLE_SIZE, cy - TRIANGLE_SIZE);
        ctx.lineTo(cx + TRIANGLE_SIZE, cy - TRIANGLE_SIZE);
    }
    ctx.closePath();
    ctx.fill();
}

// A rounded price tag anchored at (x, y) -- x is its left edge, y its vertical center -- used
// for every right-margin price readout so the whole axis reads as one visual language. Solid by
// default; hollow gives a dashed outline with no fill instead, purely to mark a draft's SL/TP as
// "not a placed order yet" -- same colour convention (bg doubles as the outline/text colour),
// distinct silhouette. direction (true/false = long/short, null/undefined = none) prefixes a
// small up/down triangle.
function drawPricePill(ctx, x, y, text, bg, textColor, font, direction, hollow) {
    ctx.font = font ?? 'bold 11px monospace';
    const hasTriangle = direction === true || direction === false;
    const hasText = !!text;
    // A triangle with no trailing text (a direction-only Entry tag) is a compact, symmetric pill
    // with the triangle dead-centered -- the wider triangleSlot only applies when text follows it.
    const triangleSlot = hasTriangle && hasText ? TRIANGLE_SLOT : 0;
    const textWidth = hasText ? ctx.measureText(text).width : 0;
    const w = hasTriangle && !hasText
        ? PILL_PAD_X * 2 + TRIANGLE_SIZE * 2
        : textWidth + PILL_PAD_X * 2 + triangleSlot;
    const resolvedTextColor = hollow ? bg : (textColor ?? textColorFor(bg));

    if (hollow) {
        ctx.save();
        ctx.setLineDash([3, 2]);
        ctx.strokeStyle = bg;
        ctx.lineWidth = 1.25;
        roundedRectPath(ctx, x + 0.5, y - PILL_HEIGHT / 2 + 0.5, w - 1, PILL_HEIGHT - 1, 3);
        ctx.stroke();
        ctx.restore();
    } else {
        roundedRectPath(ctx, x, y - PILL_HEIGHT / 2, w, PILL_HEIGHT, 3);
        ctx.fillStyle = bg;
        ctx.fill();
    }

    if (hasTriangle) {
        const triangleCx = hasText ? x + PILL_PAD_X + TRIANGLE_SIZE : x + w / 2;
        drawDirectionTriangle(ctx, triangleCx, y, direction, resolvedTextColor);
    }
    if (!hasText) return w;

    ctx.fillStyle = resolvedTextColor;
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, x + PILL_PAD_X + triangleSlot, y);
    return w;
}

// Greedily separates same-column price tags that would otherwise overlap -- e.g. an Entry a
// few cents from its own Stop Loss -- while the horizontal price line itself stays put; only
// the tag moves, with a short leader drawn back to its true level when it does.
function stackLabels(items, minY, maxY) {
    const sorted = items.slice().sort((a, b) => a.y - b.y);
    for (let i = 1; i < sorted.length; i++) {
        if (sorted[i].y < sorted[i - 1].y + PILL_MIN_GAP) sorted[i].y = sorted[i - 1].y + PILL_MIN_GAP;
    }
    const overflow = sorted.length > 0 ? sorted[sorted.length - 1].y - maxY : 0;
    if (overflow > 0) {
        sorted.forEach(item => { item.y -= overflow; });
        for (let i = sorted.length - 2; i >= 0; i--) {
            if (sorted[i].y > sorted[i + 1].y - PILL_MIN_GAP) sorted[i].y = sorted[i + 1].y - PILL_MIN_GAP;
        }
    }
    sorted.forEach(item => { item.y = Math.min(maxY, Math.max(minY, item.y)); });
    return sorted;
}

// Draws a stacked column of price tags at axisX -- leaders connect any tag that got nudged off
// its true price back to the horizontal line it belongs to.
function drawAxisColumn(ctx, specs, axisX, lineRight, minY, maxY) {
    specs.forEach(s => {
        s.rawY = s.y;
    });
    const stacked = stackLabels(specs, minY, maxY);
    stacked.forEach(s => {
        if (Math.abs(s.y - s.rawY) > 1) {
            ctx.save();
            ctx.strokeStyle = s.color ?? 'rgba(255,255,255,0.4)';
            ctx.globalAlpha = 0.6;
            ctx.beginPath();
            ctx.moveTo(lineRight, s.rawY);
            ctx.lineTo(axisX - 2, s.y);
            ctx.stroke();
            ctx.restore();
        }
        drawPricePill(ctx, axisX, s.y, s.text, s.bg, s.textColor, s.font, s.direction, s.hollow);
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
    if (bars.length === 0) return;

    const latestPrice = entry.latestPrice;
    const showHighLowLines = entry.showHighLowLines;
    const showVolumeProfile = entry.showVolumeProfile;
    const volumeProfile = entry.volumeProfile;
    const entryPrice = entry.entryPrice;
    const positionIsLong = entry.positionIsLong;
    const stopLossPrice = entry.stopLossPrice;
    const takeProfitPrice = entry.takeProfitPrice;
    const draftEntryPrice = entry.draftEntryPrice;
    const draftIsLong = entry.draftIsLong;
    const draftStopLossPrice = entry.draftStopLossPrice;
    const draftTakeProfitPrice = entry.draftTakeProfitPrice;

    const bullColor = cssVar('--color-success', '#27c93f');
    const bearColor = cssVar('--color-error', '#ff5f56');
    const latestColor = cssVar('--color-highlight', '#fff01f');
    const coverageColor = cssVar('--color-secondary', '#00d4ff');
    const volumeColor = cssVar('--color-primary', '#00ff9d');
    // Quartile/Decile/Mean+-SD bars are box plots, not a bull/bear candle -- open vs close doesn't
    // mean "up" or "down" for them, so they render in the same light blue as the coverage meter.
    const isStatBarKind = entry.barKind !== 'Ohlc';
    // Same badge colour TradeChartPanel's own open-position lines use (see marketChart.js's
    // groupColors.position), so an open position reads the same way across every chart it appears on.
    const entryColor = cssVar('--color-badge-1', '#b6ff00');
    // Same badge colour TradeChartPanel's own draft-order preview lines use (see marketChart.js's
    // groupColors.draft) -- kept for the draft Entry tag and its zone shading, so a draft still
    // reads as "draft" at a glance. Its SL/TP lines below use the real red/green instead, so the
    // risk/reward sides stay legible while a draft is being sized up.
    const draftColor = cssVar('--color-badge-3', '#ff2eea');
    const gridColor = cssVar('--border-color', 'rgba(255,255,255,0.08)');
    const labelColor = 'rgba(255,255,255,0.5)';
    const axisTickBg = 'rgba(255,255,255,0.07)';

    // Pass 1: the price range doesn't depend on the right-margin width, so it's safe to read it
    // once up front and size the price column to this frame's grid/latest tags. The position
    // column doesn't need measuring -- it's a fixed width of kind-only labels (see POSITION_COLUMN_WIDTH).
    const prelim = geometryFor(canvas, entry);
    // Direction is carried entirely by the triangle now -- "LONG"/"SHORT" text would just repeat
    // it. Only fall back to a label when there's no direction to draw a triangle for -- kept to
    // 2 letters, same width budget as the SL/TP badges, so the column stays exactly as narrow.
    const entryLabel = positionIsLong === null || positionIsLong === undefined ? 'EN' : '';
    const draftEntryLabel = draftIsLong === null || draftIsLong === undefined ? 'EN' : '';

    const gridPrices = [prelim.minPrice, (prelim.minPrice + prelim.maxPrice) / 2, prelim.maxPrice];
    const priceSpecs = gridPrices.map(p => ({ price: p, text: formatPrice(p, entry.normalize), bg: axisTickBg, textColor: labelColor, font: '11px monospace' }));
    if (latestPrice !== null) priceSpecs.push({ price: latestPrice, text: formatPrice(latestPrice, entry.normalize), bg: latestColor, color: latestColor });

    // Kind-only labels -- no price, no "DFT" -- the line itself (and, for a draft, the badge
    // colour on Entry) already says where and what it is; the tag just needs to say TP/SL/Entry.
    // A draft's SL/TP render hollow with a dashed outline instead of solid -- the one visual cue
    // that distinguishes "not placed yet" from a real, working SL/TP without adding more text.
    const positionSpecs = [];
    if (entryPrice !== null) positionSpecs.push({ price: entryPrice, text: entryLabel, bg: entryColor, color: entryColor, direction: positionIsLong });
    if (stopLossPrice !== null) positionSpecs.push({ price: stopLossPrice, text: 'SL', bg: bearColor, color: bearColor });
    if (takeProfitPrice !== null) positionSpecs.push({ price: takeProfitPrice, text: 'TP', bg: bullColor, color: bullColor });
    if (draftEntryPrice !== null) positionSpecs.push({ price: draftEntryPrice, text: draftEntryLabel, bg: draftColor, color: draftColor, direction: draftIsLong });
    if (draftStopLossPrice !== null) positionSpecs.push({ price: draftStopLossPrice, text: 'SL', bg: bearColor, color: bearColor, hollow: true });
    if (draftTakeProfitPrice !== null) positionSpecs.push({ price: draftTakeProfitPrice, text: 'TP', bg: bullColor, color: bullColor, hollow: true });

    ctx.font = 'bold 11px monospace';
    let priceColMax = 0;
    priceSpecs.forEach(s => { priceColMax = Math.max(priceColMax, ctx.measureText(s.text).width); });

    entry.priceColumnWidth = Math.min(
        MAX_PRICE_COLUMN,
        Math.max(MIN_PRICE_COLUMN, Math.ceil(priceColMax + PILL_PAD_X * 2 + AXIS_GAP + EDGE_MARGIN))
    );

    // Pass 2: real geometry, now that the price column is sized for this frame's tags.
    const { chartWidth, chartHeight, plotRight, priceColumnWidth, highlightWidth, minPrice, maxPrice, slotWidth, yFor, priceFor, xFor } =
        geometryFor(canvas, entry);
    const bodyWidth = Math.max(1, slotWidth * 0.6);
    // Position column sits closest to the chart (left of the price axis); the price axis stays
    // outermost, flush to the right edge, same as every other chart in the app.
    const lineRight = width - priceColumnWidth - COLUMN_GAP - POSITION_COLUMN_WIDTH;
    const positionAxisX = lineRight + AXIS_GAP;
    const priceAxisX = width - priceColumnWidth + AXIS_GAP;
    const columnDividerX = lineRight + POSITION_COLUMN_WIDTH + COLUMN_GAP / 2;

    // Position zones: translucent fill from Entry to SL (red) and Entry to TP (green) for the
    // open position, and the draft's own badge colour for its preview -- same idea as XGrid's
    // draft-order zone shading -- drawn first so everything else layers on top of it.
    if (entryPrice !== null) {
        const entryY = yFor(entryPrice);
        const zoneWidth = lineRight - PADDING.left;
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
        const zoneWidth = lineRight - PADDING.left;
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

    gridPrices.forEach(price => {
        const y = yFor(price);
        ctx.strokeStyle = gridColor;
        ctx.beginPath();
        ctx.moveTo(PADDING.left, y);
        ctx.lineTo(lineRight, y);
        ctx.stroke();
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
                ctx.lineTo(lineRight, y);
                ctx.stroke();
            });
        });
        ctx.restore();
    }

    bars.forEach((bar, i) => {
        const x = xFor(i);
        const isBull = bar.close >= bar.open;
        const color = isStatBarKind ? coverageColor : (isBull ? bullColor : bearColor);

        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(x, yFor(bar.high));
        ctx.lineTo(x, yFor(bar.low));
        ctx.stroke();
        ctx.lineWidth = 1;

        const openY = yFor(bar.open);
        const closeY = yFor(bar.close);
        const top = Math.min(openY, closeY);
        const bodyHeight = Math.max(2, Math.abs(closeY - openY));
        ctx.fillStyle = color;
        roundedRectPath(ctx, x - bodyWidth / 2, top, bodyWidth, bodyHeight, Math.min(2, bodyWidth / 2));
        ctx.fill();

        // Coverage meter: a binary on/off flag per bar, not a fill gauge -- on only when data
        // actually reaches back past this bar's selected lookback window, off (drawn as nothing)
        // otherwise, so there's no partial bar to misread as "mostly enough data".
        if (bar.hasFullCoverage) {
            const meterY = PADDING.top - COVERAGE_BAR_GAP - COVERAGE_BAR_HEIGHT;
            ctx.fillStyle = coverageColor;
            roundedRectPath(ctx, x - bodyWidth / 2, meterY, bodyWidth, COVERAGE_BAR_HEIGHT, COVERAGE_BAR_HEIGHT / 2);
            ctx.fill();
        }

        ctx.font = '11px monospace';
        ctx.fillStyle = labelColor;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'top';
        ctx.fillText(bar.label, x, height - PADDING.bottom + 4);
    });

    // Position/draft/latest price lines -- each stays at its true price; only its axis tag can
    // move (see drawAxisColumn below), so the line itself always lines up with where it belongs.
    // The draft Entry keeps the draft badge colour, but its SL/TP use the real red/green so both
    // sides of a draft are legible at a glance while it's being sized up.
    const drawLevelLine = (price, color, dashed) => {
        if (price === null) return;
        const y = yFor(price);
        ctx.save();
        ctx.strokeStyle = color;
        if (dashed) ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(PADDING.left, y);
        ctx.lineTo(lineRight, y);
        ctx.stroke();
        ctx.restore();
    };
    drawLevelLine(latestPrice, latestColor, true);
    drawLevelLine(entryPrice, entryColor, false);
    drawLevelLine(stopLossPrice, bearColor, true);
    drawLevelLine(takeProfitPrice, bullColor, true);
    drawLevelLine(draftEntryPrice, draftColor, true);
    drawLevelLine(draftStopLossPrice, bearColor, true);
    drawLevelLine(draftTakeProfitPrice, bullColor, true);

    // Axis tags: the position column (Entry/SL/TP + draft) is a persistent lane right against
    // the chart, always reserved -- with its divider always drawn -- so the plot doesn't resize
    // as positions and drafts come and go. The plain price column (grid ticks + latest) stays
    // outermost, flush to the right edge.
    positionSpecs.forEach(s => { s.y = yFor(s.price); });
    drawAxisColumn(ctx, positionSpecs, positionAxisX, lineRight, PADDING.top, height - PADDING.bottom);

    ctx.strokeStyle = gridColor;
    ctx.beginPath();
    ctx.moveTo(columnDividerX, PADDING.top);
    ctx.lineTo(columnDividerX, height - PADDING.bottom);
    ctx.stroke();

    priceSpecs.forEach(s => { s.y = yFor(s.price); });
    drawAxisColumn(ctx, priceSpecs, priceAxisX, lineRight, PADDING.top, height - PADDING.bottom);

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
        ctx.lineTo(lineRight, cursorY);
        ctx.stroke();
        ctx.restore();

        drawPricePill(ctx, priceAxisX, cursorY, formatPrice(cursorPrice, entry.normalize), 'rgba(10,10,10,0.85)', '#ffffff');

        ctx.font = '11px monospace';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = labelColor;
        let readout =
            `${bar.label}   O ${formatPrice(bar.open, entry.normalize)}   H ${formatPrice(bar.high, entry.normalize)}   ` +
            `L ${formatPrice(bar.low, entry.normalize)}   C ${formatPrice(bar.close, entry.normalize)}   Vol ${bar.volume.toLocaleString()}`;

        if (showHighLowLines) {
            const hlCount = bars.filter(b => cursorPrice >= b.low && cursorPrice <= b.high).length;
            readout += `   HL ${hlCount}/${bars.length}`;
        }

        if (showVolumeProfile && volumeProfile.length > 0) {
            const bucket = volumeProfile.find(b => cursorPrice >= b.low && cursorPrice <= b.high);
            readout += `   VP ${(bucket?.count ?? 0).toLocaleString()}`;
        }

        ctx.fillText(readout, PADDING.left, CROSSHAIR_TEXT_Y);
    }
}
