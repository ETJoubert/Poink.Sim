// Canvas renderer for the XGrid footprint. The Blazor component (XGrid.razor.cs) owns all
// state and hands over a per-draw payload of pre-computed intensities; this module only
// paints pixels, hit-tests the cursor, and drives the line-drawing interaction -- same
// data/pixels split the component held when it rendered to a CSS grid, moved off the DOM so
// cell count no longer costs DOM nodes.

// Theme colours live on :root; XGrid's own variables (see XGrid.razor.css) live on
// .xgrid-wrapper instead, so those calls pass the canvas to read the inherited value.
function cssVar(name, el = document.documentElement) {
    return getComputedStyle(el).getPropertyValue(name).trim();
}

// App theme colours are hex; parse to "r,g,b" so alpha can be applied per cell.
function rgb(value) {
    const s = value.trim();
    if (s.startsWith('#')) {
        let h = s.slice(1);
        if (h.length === 3) h = h.split('').map(c => c + c).join('');
        const n = parseInt(h, 16);
        return `${(n >> 16) & 255},${(n >> 8) & 255},${n & 255}`;
    }
    const m = s.match(/\d+/g);
    return m ? m.slice(0, 3).join(',') : '128,128,128';
}

function markerVar(kind) {
    switch (kind) {
        case 'poc': return '--color-primary';
        case 'vah':
        case 'val': return '--color-highlight';
        case 'vwap': return '--color-tertiary';
        case 'avg': return '--color-badge-2';
        case 'med': return '--color-neutral';
        default: return '--color-secondary';
    }
}

export function draw(canvas, dotNetRef, payload) {
    if (!canvas) return;
    if (!canvas.__xgrid) {
        canvas.__xgrid = { tipKey: null, mouse: null, hover: null, hoverMargin: null, hoverLine: null, hoverPos: null, hoverPend: null, pending: null };
        canvas.addEventListener('mousemove', e => onMove(canvas, e));
        canvas.addEventListener('mouseleave', () => onLeave(canvas));
        canvas.addEventListener('click', e => onClick(canvas, e));
        canvas.addEventListener('contextmenu', e => onContextMenu(canvas, e));
    }
    const st = canvas.__xgrid;
    st.payload = payload;
    st.dotNetRef = dotNetRef;
    paint(canvas);
}

// Repaints from the stored payload -- called on each draw and again on any hover/pending
// change so overlays layer over the already-painted footprint without a .NET round-trip.
function paint(canvas) {
    const st = canvas.__xgrid;
    const p = st.payload;
    if (!p) return;

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const W = canvas.clientWidth, H = canvas.clientHeight;
    if (W === 0 || H === 0) return;
    // Resize only when the backing store size actually changed -- the pointer crosshair
    // repaints on every mousemove, and reallocating the buffer each time would be wasteful.
    const bw = Math.round(W * dpr), bh = Math.round(H * dpr);
    if (canvas.width !== bw || canvas.height !== bh) {
        canvas.width = bw;
        canvas.height = bh;
    }
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    render(ctx, W, H, p, st, canvas);
}

// One reserved cell of margin on the left and bottom -- sizing the cells to cols+1 / rows+1
// leaves exactly one cell of gutter that stays aligned to the grid at any canvas size.
function layout(p, W, H) {
    const m = p.margins ? 1 : 0;
    const ch = H / (p.rows + m);
    if (!m) return { cw: W / p.cols, ch, ml: 0, mr: 0, mb: 0, gRight: W, gBottom: H };

    // Fixed-width left and right margins for markers and labels -- the grid takes the width
    // that remains, so both gutters stay a constant size regardless of column count.
    const margin = 28;
    const cw = (W - 2 * margin) / p.cols;
    return { cw, ch, ml: margin, mr: margin, mb: ch, gRight: W - margin, gBottom: H - ch };
}

function render(ctx, W, H, p, st, canvas) {
    ctx.clearRect(0, 0, W, H);
    const rows = p.rows, cols = p.cols;
    if (rows <= 0 || cols <= 0) return;

    const L = layout(p, W, H);
    const cw = L.cw, ch = L.ch, ml = L.ml, mr = L.mr, mb = L.mb, gRight = L.gRight;
    const gap = (cw > 3 && ch > 3) ? 1 : 0;

    ctx.fillStyle = cssVar('--xgrid-grid-fill', canvas);
    ctx.fillRect(ml, 0, gRight - ml, H - mb);

    // cells: byte per cell, 128 = zero; delta mode reads the sign, count mode is >= 128.
    const info = rgb(cssVar('--color-info'));
    const succ = rgb(cssVar('--color-success'));
    const err = rgb(cssVar('--color-error'));
    const isDelta = p.mode === 'delta';
    const cells = p.cells;
    for (let r = 0; r < rows; r++) {
        const y = r * ch;
        for (let c = 0; c < cols; c++) {
            const v = (cells[r * cols + c] - 128) / 127;
            if (v === 0) continue;
            const col = isDelta ? (v >= 0 ? succ : err) : info;
            const a = Math.min(1, Math.abs(v));
            ctx.fillStyle = `rgba(${col},${a})`;
            ctx.fillRect(ml + c * cw + gap, y + gap, cw - gap, ch - gap);
        }
    }

    // Baseline footprint: a faint border around every cell that saw a tick, so balanced and
    // empty cells stay distinguishable under the count and up/down fills.
    if (p.present && cw > 4 && ch > 4) {
        ctx.strokeStyle = 'rgba(255,255,255,0.08)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
                if (!p.present[r * cols + c]) continue;
                ctx.rect(ml + c * cw + gap + 0.5, r * ch + gap + 0.5, cw - gap - 1, ch - gap - 1);
            }
        }
        ctx.stroke();
    }

    // Top-percent highlights: a brighter border over the baseline one for each direction's
    // top-percent overlay, marking the cells its Spectrum graph's threshold line also cuts off.
    strokeMask(ctx, p.topHighlight, rows, cols, ml, cw, ch, gap, cssVar('--color-accent'), 1.5);
    strokeMask(ctx, p.upHighlight, rows, cols, ml, cw, ch, gap, cssVar('--color-success'), 1.5);
    strokeMask(ctx, p.downHighlight, rows, cols, ml, cw, ch, gap, cssVar('--color-error'), 1.5);

    if (p.sweep) {
        const warn = rgb(cssVar('--color-warning'));
        ctx.save();
        ctx.globalCompositeOperation = 'screen';
        for (let r = 0; r < rows; r++) {
            const a = p.sweep[r];
            if (a > 0) {
                ctx.fillStyle = `rgba(${warn},${Math.min(1, a)})`;
                ctx.fillRect(ml, r * ch, gRight - ml, ch);
            }
        }
        ctx.restore();
    }

    if (p.markers) {
        for (const m of p.markers) {
            const color = cssVar(markerVar(m.kind));
            const y = m.row * ch;
            ctx.strokeStyle = color;
            ctx.lineWidth = 1;
            ctx.setLineDash(m.kind === 'poc' ? [] : [3, 2]);
            hline(ctx, ml, gRight, y + 0.5);
            hline(ctx, ml, gRight, y + ch - 0.5);
            ctx.setLineDash([]);
        }
    }

    // Current price sits in the right margin as a left-pointing arrow whose tip touches the
    // grid edge, clear of the footprint it used to overlap.
    if (p.current != null) {
        const y = (p.current + 0.5) * ch;
        const bw = Math.max(3, Math.min(mr - 1, 8));
        ctx.fillStyle = cssVar('--color-accent');
        ctx.beginPath();
        ctx.moveTo(gRight, y);
        ctx.lineTo(gRight + bw, y - 5);
        ctx.lineTo(gRight + bw, y + 5);
        ctx.closePath();
        ctx.fill();
    }

    if (p.margins) drawMargins(ctx, W, H, L, p, st);
    drawLines(ctx, W, H, L, p, st);
    drawPositions(ctx, W, H, L, p, st);
    drawPending(ctx, L, p, st);
    drawDraft(ctx, L, p);

    if (st.pending) strokeCell(ctx, L, st.pending.row, st.pending.col, cssVar('--color-accent'), 1.5, [3, 2]);

    const hv = st.hover;
    if (hv && hv.row >= 0 && hv.col >= 0 && hv.row < rows && hv.col < cols)
        strokeCell(ctx, L, hv.row, hv.col, cssVar('--color-primary'), 1.5, []);

    if (st.mouse) {
        const mx = st.mouse.x, my = st.mouse.y;
        if (mx >= ml && mx <= gRight && my >= 0 && my <= L.gBottom) {
            ctx.strokeStyle = 'rgba(255,255,255,0.18)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(Math.round(mx) + 0.5, 0);
            ctx.lineTo(Math.round(mx) + 0.5, L.gBottom);
            ctx.moveTo(ml, Math.round(my) + 0.5);
            ctx.lineTo(gRight, Math.round(my) + 0.5);
            ctx.stroke();
        }
    }
}

function drawMargins(ctx, W, H, L, p, st) {
    const { cw, ch, ml, mr, mb, gRight } = L;
    const rows = p.rows, cols = p.cols;
    ctx.fillStyle = 'rgba(255,255,255,0.05)';
    ctx.fillRect(0, 0, ml, H - mb);
    ctx.fillRect(gRight, 0, mr, H - mb);
    ctx.fillRect(ml, H - mb, gRight - ml, mb);

    const on = `rgba(${rgb(cssVar('--color-accent'))},0.35)`;
    ctx.fillStyle = on;
    if (p.toggledRows) for (const r of p.toggledRows) ctx.fillRect(0, r * ch, ml, ch);
    if (p.toggledCols) for (const c of p.toggledCols) ctx.fillRect(ml + c * cw, H - mb, cw, mb);

    const hm = st.hoverMargin;
    if (hm) {
        ctx.fillStyle = 'rgba(255,255,255,0.14)';
        if (hm.side === 'left') ctx.fillRect(0, hm.index * ch, ml, ch);
        else if (hm.side === 'right') ctx.fillRect(gRight, hm.index * ch, mr, ch);
        else ctx.fillRect(ml + hm.index * cw, H - mb, cw, mb);
    }

    ctx.strokeStyle = 'rgba(255,255,255,0.12)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(ml + 0.5, 0);
    ctx.lineTo(ml + 0.5, H - mb);
    ctx.moveTo(gRight + 0.5, 0);
    ctx.lineTo(gRight + 0.5, H - mb);
    ctx.moveTo(ml, H - mb + 0.5);
    ctx.lineTo(gRight, H - mb + 0.5);
    for (let r = 1; r < rows; r++) {
        const y = r * ch + 0.5;
        ctx.moveTo(0, y); ctx.lineTo(ml, y);
        ctx.moveTo(gRight, y); ctx.lineTo(W, y);
    }
    for (let c = 1; c < cols; c++) { const x = ml + c * cw + 0.5; ctx.moveTo(x, H - mb); ctx.lineTo(x, H); }
    ctx.stroke();
}

function drawLines(ctx, W, H, L, p, st) {
    if (!p.lines) return;
    const base = cssVar('--color-secondary');
    const ext = cssVar('--color-tertiary');
    const hot = cssVar('--color-accent');
    for (let i = 0; i < p.lines.length; i++) {
        const ln = p.lines[i];
        const pts = linePoints(ln, L, W, H);
        const hovered = st.hoverLine && st.hoverLine.i === i;
        ctx.strokeStyle = hovered ? hot : (ln.external ? ext : base);
        ctx.lineWidth = hovered ? 2.5 : 1.5;
        ctx.beginPath();
        ctx.moveTo(pts[0], pts[1]);
        ctx.lineTo(pts[2], pts[3]);
        ctx.stroke();
        if (ln.kind === 'segment') {
            strokeCell(ctx, L, Math.floor(ln.y1), Math.floor(ln.x1), hot, 1, []);
            strokeCell(ctx, L, Math.floor(ln.y2), Math.floor(ln.x2), hot, 1, []);
        }
    }
}

// Coordinates arrive continuous in cell units (x in 0..cols, y in 0..rows) so a line sits
// exactly on its re-mapped price/time rather than snapping to a cell centre.
function linePoints(ln, L, W, H) {
    if (ln.kind === 'vertical') {
        const x = L.ml + ln.x1 * L.cw;
        return [x, 0, x, L.gBottom];
    }
    if (ln.kind === 'horizontal') {
        const y = ln.y1 * L.ch;
        return [L.ml, y, L.gRight, y];
    }
    return [
        L.ml + ln.x1 * L.cw, ln.y1 * L.ch,
        L.ml + ln.x2 * L.cw, ln.y2 * L.ch,
    ];
}

// state 1 = favourable (green), -1 = adverse (red), 0 = flat. Open trades run their line to
// the current price and carry a right-edge direction marker; closed trades cap it with a
// diamond at the exit.
function drawPositions(ctx, W, H, L, p, st) {
    if (!p.positions) return;
    const succ = cssVar('--color-success'), err = cssVar('--color-error'), flat = cssVar('--color-secondary');
    for (let i = 0; i < p.positions.length; i++) {
        const ps = p.positions[i];
        const color = ps.state > 0 ? succ : ps.state < 0 ? err : flat;
        const ox = L.ml + ps.ox * L.cw, oy = ps.oy * L.ch;
        const cx = L.ml + ps.cx * L.cw, cy = ps.cy * L.ch;
        const hovered = st.hoverPos && st.hoverPos.pi === i;

        ctx.strokeStyle = color;
        ctx.lineWidth = hovered ? 2.5 : 1.5;
        ctx.setLineDash([4, 3]);
        ctx.beginPath();
        ctx.moveTo(ox, oy);
        ctx.lineTo(cx, cy);
        ctx.stroke();
        ctx.setLineDash([]);

        posTriangle(ctx, ox, oy, ps.dir, color, 5);
        if (!ps.open) diamond(ctx, cx, cy, color, 4);
        if (ps.open) rightMarker(ctx, L, ps.entryY * L.ch, ps.dir, color);
        if (ps.slY != null) levelLine(ctx, L, ps.slY, err);
        if (ps.tpY != null) levelLine(ctx, L, ps.tpY, succ);
    }
}

// A held stop-loss / take-profit level: dashed line across the grid, kept while the trade is
// open so the exit targets stay visible.
function levelLine(ctx, L, yFrac, color) {
    const y = yFrac * L.ch;
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.25;
    ctx.setLineDash([4, 3]);
    ctx.beginPath();
    ctx.moveTo(L.ml, y);
    ctx.lineTo(L.gRight, y);
    ctx.stroke();
    ctx.setLineDash([]);
}

function posTriangle(ctx, x, y, dir, color, s) {
    ctx.fillStyle = color;
    ctx.beginPath();
    if (dir === 'long') { ctx.moveTo(x, y - s); ctx.lineTo(x - s, y + s); ctx.lineTo(x + s, y + s); }
    else { ctx.moveTo(x, y + s); ctx.lineTo(x - s, y - s); ctx.lineTo(x + s, y - s); }
    ctx.closePath();
    ctx.fill();
}

function diamond(ctx, x, y, color, s) {
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.moveTo(x, y - s);
    ctx.lineTo(x + s, y);
    ctx.lineTo(x, y + s);
    ctx.lineTo(x - s, y);
    ctx.closePath();
    ctx.fill();
}

function rightMarker(ctx, L, y, dir, color) {
    const s = 5, x = L.gRight + L.mr / 2;
    ctx.fillStyle = color;
    ctx.beginPath();
    if (dir === 'long') { ctx.moveTo(x, y - s); ctx.lineTo(x - s, y + s); ctx.lineTo(x + s, y + s); }
    else { ctx.moveTo(x, y + s); ctx.lineTo(x - s, y - s); ctx.lineTo(x + s, y - s); }
    ctx.closePath();
    ctx.fill();
}

function hitPosition(p, L, x, y) {
    if (!p.positions) return null;
    let best = null, bestD = 6;
    const rx = L.gRight + L.mr / 2;
    for (let i = 0; i < p.positions.length; i++) {
        const ps = p.positions[i];
        const ox = L.ml + ps.ox * L.cw, oy = ps.oy * L.ch, cx = L.ml + ps.cx * L.cw, cy = ps.cy * L.ch;
        const d = distToSeg(x, y, ox, oy, cx, cy);
        if (d < bestD) { bestD = d; best = { pi: i }; }
        if (ps.open && Math.abs(x - rx) < 8 && Math.abs(y - ps.entryY * L.ch) < 8) { best = { pi: i }; bestD = 0; }
    }
    return best;
}

// Resting orders: a dotted level in the pending colour with a right-edge direction marker,
// distinct from the green/red of live positions.
function drawPending(ctx, L, p, st) {
    if (!p.pending) return;
    const color = cssVar('--color-warning');
    for (let i = 0; i < p.pending.length; i++) {
        const pd = p.pending[i];
        const y = pd.y * L.ch;
        const hovered = st.hoverPend && st.hoverPend.qi === i;
        ctx.strokeStyle = color;
        ctx.lineWidth = hovered ? 2 : 1.25;
        ctx.setLineDash([2, 3]);
        ctx.beginPath();
        ctx.moveTo(L.ml, y);
        ctx.lineTo(L.gRight, y);
        ctx.stroke();
        ctx.setLineDash([]);
        rightMarker(ctx, L, y, pd.dir, color);
    }
}

function hitPending(p, L, x, y) {
    if (!p.pending) return null;
    const rx = L.gRight + L.mr / 2;
    let best = null, bestD = 5;
    for (let i = 0; i < p.pending.length; i++) {
        const yy = p.pending[i].y * L.ch;
        const dy = Math.abs(y - yy);
        if (x >= L.ml && dy < bestD) { bestD = dy; best = { qi: i }; }
        if (Math.abs(x - rx) < 8 && dy < 8) { best = { qi: i }; bestD = 0; }
    }
    return best;
}

// Order draft levels drawn across the grid: entry as a solid grey line, stop loss / take
// profit as dashed red / green lines. Prices are re-mapped each render so they hold position.
function drawDraft(ctx, L, p) {
    if (!p.draft || !p.draft.lines) return;
    for (const dl of p.draft.lines) {
        const y = dl.y * L.ch;
        ctx.strokeStyle = dl.kind === 'sl' ? cssVar('--color-error')
            : dl.kind === 'tp' ? cssVar('--color-success') : 'rgba(200,200,200,0.9)';
        ctx.lineWidth = 1.5;
        ctx.setLineDash(dl.kind === 'entry' ? [] : [4, 3]);
        ctx.beginPath();
        ctx.moveTo(L.ml, y);
        ctx.lineTo(L.gRight, y);
        ctx.stroke();
        ctx.setLineDash([]);
    }
}

// Strokes a border around every cell flagged in a rows*cols byte mask (present/topHighlight/
// upHighlight/downHighlight) -- shared by the several masks BuildPayload sends so each only
// differs by colour, not by loop.
function strokeMask(ctx, mask, rows, cols, ml, cw, ch, gap, color, width) {
    if (!mask || cw <= 4 || ch <= 4) return;
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.beginPath();
    for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
            if (!mask[r * cols + c]) continue;
            ctx.rect(ml + c * cw + gap + 0.5, r * ch + gap + 0.5, cw - gap - 1, ch - gap - 1);
        }
    }
    ctx.stroke();
}

function strokeCell(ctx, L, row, col, color, width, dash) {
    const gap = (L.cw > 3 && L.ch > 3) ? 1 : 0;
    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.setLineDash(dash);
    ctx.strokeRect(L.ml + col * L.cw + gap + 0.5, row * L.ch + gap + 0.5, L.cw - gap - 1, L.ch - gap - 1);
    ctx.restore();
}

function hline(ctx, x0, x1, y) {
    ctx.beginPath();
    ctx.moveTo(x0, y);
    ctx.lineTo(x1, y);
    ctx.stroke();
}

function distToSeg(px, py, x1, y1, x2, y2) {
    const dx = x2 - x1, dy = y2 - y1;
    const len2 = dx * dx + dy * dy;
    let t = len2 === 0 ? 0 : ((px - x1) * dx + (py - y1) * dy) / len2;
    t = Math.max(0, Math.min(1, t));
    return Math.hypot(px - (x1 + t * dx), py - (y1 + t * dy));
}

function hitLine(p, L, W, H, x, y) {
    if (!p.lines) return null;
    let best = null, bestD = 5;
    for (let i = 0; i < p.lines.length; i++) {
        const pts = linePoints(p.lines[i], L, W, H);
        const d = distToSeg(x, y, pts[0], pts[1], pts[2], pts[3]);
        if (d < bestD) { bestD = d; best = { i, line: p.lines[i] }; }
    }
    return best;
}

function region(p, L, x, y) {
    if (x >= L.ml && x < L.gRight && y < L.gBottom) {
        const col = Math.floor((x - L.ml) / L.cw), row = Math.floor(y / L.ch);
        if (col >= 0 && row >= 0 && col < p.cols && row < p.rows) return { type: 'cell', row, col };
    } else if (p.margins && x < L.ml && y < L.gBottom) {
        const row = Math.floor(y / L.ch);
        if (row >= 0 && row < p.rows) return { type: 'left', index: row };
    } else if (p.margins && y >= L.gBottom && x >= L.ml && x < L.gRight) {
        const col = Math.floor((x - L.ml) / L.cw);
        if (col >= 0 && col < p.cols) return { type: 'bottom', index: col };
    } else if (p.margins && x >= L.gRight && y < L.gBottom) {
        const row = Math.floor(y / L.ch);
        if (row >= 0 && row < p.rows) return { type: 'right', index: row };
    }
    return null;
}

function tipEl(canvas) {
    return canvas.parentElement ? canvas.parentElement.querySelector('.xgrid-cell-tip') : null;
}

function onMove(canvas, e) {
    const st = canvas.__xgrid;
    const p = st.payload;
    if (!p) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left, y = e.clientY - rect.top;
    st.mouse = { x, y };
    const W = rect.width, H = rect.height;
    const L = layout(p, W, H);

    const line = hitLine(p, L, W, H, x, y);
    const pos = line ? null : hitPosition(p, L, x, y);
    const pend = (line || pos) ? null : hitPending(p, L, x, y);
    const reg = (line || pos || pend) ? null : region(p, L, x, y);

    const newLine = line ? { i: line.i } : null;
    const newPos = pos ? { pi: pos.pi } : null;
    const newPend = pend ? { qi: pend.qi } : null;
    const newCell = reg && reg.type === 'cell' ? { row: reg.row, col: reg.col } : null;
    const newMargin = reg && (reg.type === 'left' || reg.type === 'bottom' || reg.type === 'right')
        ? { side: reg.type, index: reg.index } : null;

    st.hoverLine = newLine;
    st.hoverPos = newPos;
    st.hoverPend = newPend;
    st.hover = newCell;
    st.hoverMargin = newMargin;
    // Repaint every move so the pointer crosshair tracks the cursor, not only on hover change.
    paint(canvas);

    canvas.style.cursor = (newLine || newPos || newPend || newMargin) ? 'pointer' : (newCell && p.lineMode ? 'crosshair' : 'default');

    const tip = tipEl(canvas);
    if (!tip) return;
    if (newLine) {
        const ln = p.lines[newLine.i];
        if (st.tipKey !== 'L' + newLine.i) {
            st.tipKey = 'L' + newLine.i;
            tip.textContent = ln.tip || '';
            tip.style.display = ln.tip ? 'block' : 'none';
        }
        if (ln.tip) positionTip(canvas, tip);
    } else if (newPos) {
        const ps = p.positions[newPos.pi];
        if (st.tipKey !== 'P' + newPos.pi) {
            st.tipKey = 'P' + newPos.pi;
            tip.textContent = ps.tip || '';
            tip.style.display = ps.tip ? 'block' : 'none';
        }
        if (ps.tip) positionTip(canvas, tip);
    } else if (newPend) {
        const pd = p.pending[newPend.qi];
        if (st.tipKey !== 'Q' + newPend.qi) {
            st.tipKey = 'Q' + newPend.qi;
            tip.textContent = pd.tip || '';
            tip.style.display = pd.tip ? 'block' : 'none';
        }
        if (pd.tip) positionTip(canvas, tip);
    } else if (newCell) {
        showTip(canvas, tip, newCell.row + 'x' + newCell.col,
            () => st.dotNetRef.invokeMethodAsync('CellTooltip', newCell.row, newCell.col));
    } else if (newMargin) {
        const text = newMargin.side === 'left'
            ? 'Click to draw a horizontal line at this price'
            : newMargin.side === 'bottom'
                ? 'Click to draw a vertical line at this time'
                : p.orderArm === 'sl' ? 'Click to set the stop loss at this price'
                    : p.orderArm === 'tp' ? 'Click to set the take profit at this price'
                        : 'Click to draft an order at this price';
        if (st.tipKey !== 'M' + newMargin.side + newMargin.index) {
            st.tipKey = 'M' + newMargin.side + newMargin.index;
            tip.textContent = text;
            tip.style.display = 'block';
        }
        positionTip(canvas, tip);
    } else {
        hideTip(canvas);
    }
}

function showTip(canvas, tip, key, resolve) {
    const st = canvas.__xgrid;
    if (st.tipKey !== key) {
        st.tipKey = key;
        resolve().then(text => {
            if (!text) { hideTip(canvas); return; }
            tip.textContent = text;
            tip.style.display = 'block';
            positionTip(canvas, tip);
        });
    } else if (tip.style.display === 'block') {
        positionTip(canvas, tip);
    }
}

function positionTip(canvas, tip) {
    const st = canvas.__xgrid;
    if (!st.mouse) return;
    const flip = st.mouse.x > canvas.clientWidth - 150;
    tip.style.left = st.mouse.x + 'px';
    tip.style.top = st.mouse.y + 'px';
    tip.style.transform = flip ? 'translate(calc(-100% - 12px), 12px)' : 'translate(12px, 12px)';
}

function onClick(canvas, e) {
    const st = canvas.__xgrid;
    const p = st.payload;
    if (!p) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left, y = e.clientY - rect.top;
    const L = layout(p, rect.width, rect.height);
    const reg = region(p, L, x, y);
    if (!reg) return;

    if (reg.type === 'left') {
        st.dotNetRef.invokeMethodAsync('ToggleHorizontalLine', reg.index);
        return;
    }
    if (reg.type === 'bottom') {
        st.dotNetRef.invokeMethodAsync('ToggleVerticalLine', reg.index);
        return;
    }
    if (reg.type === 'right') {
        st.dotNetRef.invokeMethodAsync('RightMarginClick', reg.index);
        return;
    }
    if (!p.lineMode) return;
    if (!st.pending) {
        st.pending = { row: reg.row, col: reg.col };
        paint(canvas);
    } else {
        st.dotNetRef.invokeMethodAsync('AddUserLine', st.pending.row, st.pending.col, reg.row, reg.col);
        st.pending = null;
        paint(canvas);
    }
}

// Right-click cancels an in-progress segment first, otherwise removes a hovered user line;
// host-supplied lines carry no index and stay put.
function onContextMenu(canvas, e) {
    const st = canvas.__xgrid;
    const p = st.payload;
    if (!p) return;
    if (st.pending) { e.preventDefault(); st.pending = null; paint(canvas); return; }
    const rect = canvas.getBoundingClientRect();
    const L = layout(p, rect.width, rect.height);
    const hit = hitLine(p, L, rect.width, rect.height, e.clientX - rect.left, e.clientY - rect.top);
    if (hit && !hit.line.external && hit.line.index >= 0) {
        e.preventDefault();
        st.dotNetRef.invokeMethodAsync('RemoveUserLine', hit.line.index);
    }
}

function sig(h) {
    if (!h) return '';
    if ('pi' in h) return 'P' + h.pi;
    if ('qi' in h) return 'Q' + h.qi;
    if ('i' in h) return 'L' + h.i;
    if ('side' in h) return h.side + h.index;
    return h.row + 'x' + h.col;
}

function hideTip(canvas) {
    const st = canvas.__xgrid;
    if (st) st.tipKey = null;
    const tip = tipEl(canvas);
    if (tip) tip.style.display = 'none';
}

function onLeave(canvas) {
    const st = canvas.__xgrid;
    if (!st) return;
    st.hover = null;
    st.hoverMargin = null;
    st.hoverLine = null;
    st.hoverPos = null;
    st.hoverPend = null;
    st.mouse = null;
    paint(canvas);
    canvas.style.cursor = 'default';
    hideTip(canvas);
}
