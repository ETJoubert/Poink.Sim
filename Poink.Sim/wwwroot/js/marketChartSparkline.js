// Standalone sparkline of a bar list's close prices, with an optional highlighted
// Lookback Window the user can drag -- unlike marketChart.js's own minimap, this has
// no lightweight-charts dependency and isn't tied to any chart instance; it just draws
// whatever ProfilerPage last pushed via setBars/setWindow. Per-instance state keyed by
// the container element itself (WeakMap, so a disposed/GC'd container needs no
// explicit removal from a shared map).
const state = new WeakMap();

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const edgeZonePx = 6;
const minWindowWidth = 1;

// Index range [startIndex, endIndex] the window covers, plus its pixel bounds --
// null while no window is set (windowWidth/windowOffset are null) or there are no bars.
function windowBounds(s) {
    if (!s.bars || s.bars.length === 0 || s.windowWidth == null || s.windowOffset == null) return null;

    const rect = s.canvas.getBoundingClientRect();
    const span = Math.max(1, s.bars.length - 1);
    const endIndex = clamp(s.bars.length - 1 - s.windowOffset, 0, s.bars.length - 1);
    const startIndex = clamp(endIndex - s.windowWidth + 1, 0, endIndex);
    const xFor = i => (i / span) * rect.width;

    return { startIndex, endIndex, span, rectWidth: rect.width, left: xFor(startIndex), right: xFor(endIndex) };
}

function draw(container) {
    const s = state.get(container);
    if (!s) return;

    const { canvas, bars } = s;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));

    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, rect.width, rect.height);

    if (!bars || bars.length === 0 || rect.width === 0 || rect.height === 0) return;

    let min = Infinity, max = -Infinity;
    for (const b of bars) {
        if (b.close < min) min = b.close;
        if (b.close > max) max = b.close;
    }
    if (min === max) { min -= 1; max += 1; }

    const padY = rect.height * 0.12;
    const plotH = rect.height - padY * 2;
    const yForClose = c => padY + plotH - ((c - min) / (max - min)) * plotH;
    const xForIndex = i => (i / Math.max(1, bars.length - 1)) * rect.width;

    const tracePath = new Path2D();
    bars.forEach((b, i) => {
        const x = xForIndex(i);
        const y = yForClose(b.close);
        if (i === 0) tracePath.moveTo(x, y); else tracePath.lineTo(x, y);
    });

    const areaPath = new Path2D(tracePath);
    areaPath.lineTo(rect.width, rect.height);
    areaPath.lineTo(0, rect.height);
    areaPath.closePath();

    // Same --color-accent convention as marketChart.js's own minimap sparkline trace.
    const color = getComputedStyle(container).getPropertyValue('--color-accent').trim() || '#ff6b9d';

    ctx.save();
    ctx.globalAlpha = 0.12;
    ctx.fillStyle = color;
    ctx.fill(areaPath);
    ctx.restore();

    ctx.save();
    ctx.globalAlpha = 0.7;
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.25;
    ctx.stroke(tracePath);
    ctx.restore();

    const bounds = windowBounds(s);
    if (!bounds) return;

    const windowColor = getComputedStyle(container).getPropertyValue('--color-info').trim() || '#4dc3ff';
    const left = bounds.left, right = Math.max(bounds.left + 1, bounds.right);

    ctx.save();
    ctx.globalAlpha = 0.22;
    ctx.fillStyle = windowColor;
    ctx.fillRect(left, 0, right - left, rect.height);
    ctx.restore();

    ctx.save();
    ctx.strokeStyle = windowColor;
    ctx.lineWidth = 1;
    ctx.strokeRect(left + 0.5, 0.5, Math.max(1, right - left - 1), rect.height - 1);
    ctx.restore();
}

function hitTest(bounds, xCss) {
    if (Math.abs(xCss - bounds.left) <= edgeZonePx) return 'resize-left';
    if (Math.abs(xCss - bounds.right) <= edgeZonePx) return 'resize-right';
    if (xCss > bounds.left && xCss < bounds.right) return 'pan';
    return null;
}

// Reports width/offset to .NET on every actual change during a drag (not just on
// release), so ProfilerPage's dependent panels update live as the user drags.
function reportWindow(s) {
    s.dotNetRef?.invokeMethodAsync('OnWindowChangedAsync', s.windowWidth, s.windowOffset);
}

// The window's body is draggable to pan it (offset only); either edge is draggable
// to resize it (width and, for the right edge, offset too) -- both live per pointermove.
function attachDragInteractions(container, s) {
    const canvas = s.canvas;

    canvas.addEventListener('pointermove', e => {
        const rect = canvas.getBoundingClientRect();
        const xCss = e.clientX - rect.left;

        if (s.dragMode) {
            const start = s.dragStartBounds;
            const deltaIndex = ((xCss - s.dragStartX) / start.rectWidth) * start.span;
            let startIndex = start.startIndex, endIndex = start.endIndex;

            if (s.dragMode === 'pan') {
                const width = start.endIndex - start.startIndex;
                const maxStart = s.bars.length - 1 - width;
                startIndex = clamp(Math.round(start.startIndex + deltaIndex), 0, maxStart);
                endIndex = startIndex + width;
            } else if (s.dragMode === 'resize-left') {
                startIndex = clamp(Math.round(start.startIndex + deltaIndex), 0, endIndex - minWindowWidth + 1);
            } else if (s.dragMode === 'resize-right') {
                endIndex = clamp(Math.round(start.endIndex + deltaIndex), startIndex + minWindowWidth - 1, s.bars.length - 1);
            }

            const width = endIndex - startIndex + 1;
            const offset = s.bars.length - 1 - endIndex;
            if (width !== s.windowWidth || offset !== s.windowOffset) {
                s.windowWidth = width;
                s.windowOffset = offset;
                draw(container);
                reportWindow(s);
            }
            return;
        }

        const bounds = windowBounds(s);
        const hit = bounds ? hitTest(bounds, xCss) : null;
        canvas.style.cursor = hit === 'resize-left' || hit === 'resize-right' ? 'ew-resize'
            : hit === 'pan' ? 'grab' : 'default';
    });

    canvas.addEventListener('pointerdown', e => {
        const rect = canvas.getBoundingClientRect();
        const xCss = e.clientX - rect.left;
        const bounds = windowBounds(s);
        const hit = bounds ? hitTest(bounds, xCss) : null;
        if (!hit) return;

        s.dragMode = hit;
        s.dragStartX = xCss;
        s.dragStartBounds = bounds;
        canvas.setPointerCapture(e.pointerId);
        canvas.style.cursor = hit === 'pan' ? 'grabbing' : 'ew-resize';
    });

    function endDrag(e) {
        if (!s.dragMode) return;
        s.dragMode = null;
        s.dragStartBounds = null;
        try { canvas.releasePointerCapture(e.pointerId); } catch { /* already released */ }
        canvas.style.cursor = 'default';
    }
    canvas.addEventListener('pointerup', endDrag);
    canvas.addEventListener('pointercancel', endDrag);
}

export function create(container, dotNetRef) {
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'display:block;width:100%;height:100%;';
    container.appendChild(canvas);

    const s = {
        canvas, bars: [], windowWidth: null, windowOffset: null, dotNetRef,
        dragMode: null, dragStartX: 0, dragStartBounds: null,
    };
    state.set(container, s);

    const resizeObserver = new ResizeObserver(() => draw(container));
    resizeObserver.observe(container);
    s.resizeObserver = resizeObserver;

    attachDragInteractions(container, s);
}

export function setBars(container, bars) {
    const s = state.get(container);
    if (!s) return;
    s.bars = bars;
    draw(container);
}

export function setWindow(container, width, offset) {
    const s = state.get(container);
    if (!s) return;
    s.windowWidth = width;
    s.windowOffset = offset;
    draw(container);
}

export function dispose(container) {
    const s = state.get(container);
    if (!s) return;
    s.resizeObserver.disconnect();
    s.canvas.remove();
    state.delete(container);
}
