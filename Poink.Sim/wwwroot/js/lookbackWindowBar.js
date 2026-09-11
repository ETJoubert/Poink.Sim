// Draggable "LOOKBACK WINDOW" rail shared by SimulationPage and TradeChartPanel -- the
// highlighted segment (`.lkbk-fill`) is a plain DOM element, not canvas, and its geometry
// comes straight from Width/Offset/MaxWindow (kept on the rail's dataset by Blazor on every
// render) rather than the bar-indexed math marketChartSparkline.js uses for its sparkline.
// Per-instance drag state keyed by the rail element itself (WeakMap, no explicit cleanup
// needed for a disposed/GC'd rail).
const state = new WeakMap();

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const edgeZonePx = 6;

function readConfig(rail) {
    return {
        max: parseFloat(rail.dataset.max) || 0,
        width: parseFloat(rail.dataset.width) || 0,
        offset: parseFloat(rail.dataset.offset) || 0,
        minWidth: parseFloat(rail.dataset.minWidth) || 0,
    };
}

// Pixel bounds of the highlighted segment -- its right edge sits `offset` back from the
// rail's right edge ("now"), its left edge `offset + width` back.
function bounds(rail) {
    const cfg = readConfig(rail);
    if (cfg.max <= 0) return null;

    const rect = rail.getBoundingClientRect();
    const xFor = distanceBack => rect.width * (1 - distanceBack / cfg.max);

    return { ...cfg, rectWidth: rect.width, left: xFor(cfg.offset + cfg.width), right: xFor(cfg.offset) };
}

// Capped at a third of the fill's own width so a narrow window (near MinWidth) can't let
// the two edge zones swallow the whole bar and make its body un-clickable for panning.
function hitTest(b, xCss) {
    const zone = Math.min(edgeZonePx, (b.right - b.left) / 3);
    if (Math.abs(xCss - b.right) <= zone) return 'resize-right';
    if (Math.abs(xCss - b.left) <= zone) return 'resize-left';
    if (xCss > b.left && xCss < b.right) return 'pan';
    return null;
}

// Coalescing reporter -- Width/Offset drive expensive downstream work on the .NET side
// (chart/XGrid/volume-profile/time-profile recompute), so firing invokeMethodAsync on every
// pointermove (up to ~60/sec) can queue calls faster than the server can process them, which
// looks like the drag "getting stuck" as stale queued values slowly drain. This keeps at most
// one call in flight and always sends the latest known position once the server is ready for
// it, dropping any values that went stale in between instead of draining through them.
function reportWindow(s, width, offset) {
    s.pendingReport = { width, offset };
    if (s.reporting) return;

    s.reporting = true;
    const sendNext = () => {
        const next = s.pendingReport;
        s.pendingReport = null;
        s.dotNetRef
            .invokeMethodAsync('OnWindowChangedAsync', next.width, next.offset)
            .catch(() => { /* circuit hiccup -- next drag will resync from the dataset */ })
            .finally(() => {
                if (s.pendingReport) sendNext(); else s.reporting = false;
            });
    };
    sendNext();
}

// Either edge of the rail is draggable to resize it, anchoring the opposite edge (dragging
// the right edge changes Offset, dragging the left edge changes Width); the highlighted
// bar's body is draggable to pan it (Offset only, Width fixed).
function attachDragInteractions(rail, s) {
    s.onPointerMove = e => {
        const rect = rail.getBoundingClientRect();
        const xCss = e.clientX - rect.left;

        if (s.dragMode) {
            const start = s.dragStart;
            const deltaBack = -((xCss - s.dragStartX) / start.rectWidth) * start.max;
            let offset = start.offset, width = start.width;

            if (s.dragMode === 'resize-right') {
                const farEdge = start.offset + start.width;
                offset = clamp(start.offset + deltaBack, 0, farEdge - start.minWidth);
                width = farEdge - offset;
            } else if (s.dragMode === 'resize-left') {
                width = clamp(start.width + deltaBack, start.minWidth, start.max - start.offset);
            } else if (s.dragMode === 'pan') {
                offset = clamp(start.offset + deltaBack, 0, Math.max(0, start.max - start.width));
            }

            if (s.dotNetRef) reportWindow(s, width, offset);
            return;
        }

        const b = bounds(rail);
        const hit = b ? hitTest(b, xCss) : null;
        rail.style.cursor = hit === 'resize-right' || hit === 'resize-left' ? 'ew-resize'
            : hit === 'pan' ? 'grab' : 'default';
    };

    s.onPointerDown = e => {
        const rect = rail.getBoundingClientRect();
        const xCss = e.clientX - rect.left;
        const b = bounds(rail);
        const hit = b ? hitTest(b, xCss) : null;
        if (!hit) return;

        // Without this the browser's own default action (text selection / native drag-start)
        // can hijack the gesture, which swallows the pointermove stream on the first press --
        // a second click then works because that default action never gets the chance to arm.
        e.preventDefault();

        s.dragMode = hit;
        s.dragStartX = xCss;
        s.dragStart = b;
        rail.setPointerCapture(e.pointerId);
        rail.classList.add('lkbk-rail-dragging');
        rail.style.cursor = hit === 'pan' ? 'grabbing' : 'ew-resize';
    };

    s.onPointerEnd = e => {
        if (!s.dragMode) return;
        s.dragMode = null;
        s.dragStart = null;
        try { rail.releasePointerCapture(e.pointerId); } catch { /* already released */ }
        rail.classList.remove('lkbk-rail-dragging');
        rail.style.cursor = 'default';
    };

    rail.addEventListener('pointermove', s.onPointerMove);
    rail.addEventListener('pointerdown', s.onPointerDown);
    rail.addEventListener('pointerup', s.onPointerEnd);
    rail.addEventListener('pointercancel', s.onPointerEnd);
}

export function create(rail, dotNetRef) {
    const s = { dotNetRef, dragMode: null, dragStartX: 0, dragStart: null };
    state.set(rail, s);
    attachDragInteractions(rail, s);
}

export function dispose(rail) {
    const s = state.get(rail);
    if (!s) return;

    rail.removeEventListener('pointermove', s.onPointerMove);
    rail.removeEventListener('pointerdown', s.onPointerDown);
    rail.removeEventListener('pointerup', s.onPointerEnd);
    rail.removeEventListener('pointercancel', s.onPointerEnd);
    state.delete(rail);
}
