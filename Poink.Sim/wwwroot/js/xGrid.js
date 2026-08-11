// Canvas renderer for the XGrid footprint. The Blazor component (XGrid.razor.cs) owns all
// state and hands over a per-draw payload of pre-computed intensities; this module only
// paints pixels, hit-tests the cursor, and drives the line-drawing interaction -- same
// data/pixels split the component held when it rendered to a CSS grid, moved off the DOM so
// cell count no longer costs DOM nodes.

function cssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
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
        canvas.__xgrid = { tipKey: null, mouse: null, hover: null, hoverMargin: null, hoverLine: null, hoverPos: null, pending: null };
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
    canvas.width = Math.round(W * dpr);
    canvas.height = Math.round(H * dpr);
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    render(ctx, W, H, p, st);
}

// One reserved cell of margin on the left and bottom -- sizing the cells to cols+1 / rows+1
// leaves exactly one cell of gutter that stays aligned to the grid at any canvas size.
function layout(p, W, H) {
    const m = p.margins ? 1 : 0;
    const ch = H / (p.rows + m);
    if (!m) return { cw: W / p.cols, ch, ml: 0, mr: 0, mb: 0, gRight: W, gBottom: H };

    // Right margin only needs room for the current-price and position markers -- cap it so a
    // few wide columns don't hand it a full cell's width; the grid reclaims the surplus.
    const maxRight = 28;
    let cw = W / (p.cols + 2);
    let mr = cw;
    if (mr > maxRight) {
        mr = maxRight;
        cw = (W - mr) / (p.cols + 1);
    }
    return { cw, ch, ml: cw, mr, mb: ch, gRight: W - mr, gBottom: H - ch };
}

function render(ctx, W, H, p, st) {
    ctx.clearRect(0, 0, W, H);
    const rows = p.rows, cols = p.cols;
    if (rows <= 0 || cols <= 0) return;

    const L = layout(p, W, H);
    const cw = L.cw, ch = L.ch, ml = L.ml, mr = L.mr, mb = L.mb, gRight = L.gRight;
    const gap = (cw > 3 && ch > 3) ? 1 : 0;

    ctx.fillStyle = 'rgba(255,255,255,0.03)';
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

    if (st.pending) strokeCell(ctx, L, st.pending.row, st.pending.col, cssVar('--color-accent'), 1.5, [3, 2]);

    const hv = st.hover;
    if (hv && hv.row >= 0 && hv.col >= 0 && hv.row < rows && hv.col < cols)
        strokeCell(ctx, L, hv.row, hv.col, cssVar('--color-primary'), 1.5, []);
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
    }
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
    const reg = (line || pos) ? null : region(p, L, x, y);

    const newLine = line ? { i: line.i } : null;
    const newPos = pos ? { pi: pos.pi } : null;
    const newCell = reg && reg.type === 'cell' ? { row: reg.row, col: reg.col } : null;
    const newMargin = reg && (reg.type === 'left' || reg.type === 'bottom')
        ? { side: reg.type, index: reg.index } : null;

    let changed = false;
    if (sig(st.hoverLine) !== sig(newLine)) { st.hoverLine = newLine; changed = true; }
    if (sig(st.hoverPos) !== sig(newPos)) { st.hoverPos = newPos; changed = true; }
    if (sig(st.hover) !== sig(newCell)) { st.hover = newCell; changed = true; }
    if (sig(st.hoverMargin) !== sig(newMargin)) { st.hoverMargin = newMargin; changed = true; }
    if (changed) paint(canvas);

    canvas.style.cursor = (newLine || newPos || newMargin) ? 'pointer' : (newCell && p.lineMode ? 'crosshair' : 'default');

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
    } else if (newCell) {
        showTip(canvas, tip, newCell.row + 'x' + newCell.col,
            () => st.dotNetRef.invokeMethodAsync('CellTooltip', newCell.row, newCell.col));
    } else if (newMargin) {
        const text = newMargin.side === 'left'
            ? 'Click to draw a horizontal line at this price'
            : 'Click to draw a vertical line at this time';
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
    let changed = false;
    if (st.hover) { st.hover = null; changed = true; }
    if (st.hoverMargin) { st.hoverMargin = null; changed = true; }
    if (st.hoverLine) { st.hoverLine = null; changed = true; }
    if (st.hoverPos) { st.hoverPos = null; changed = true; }
    if (changed) paint(canvas);
    canvas.style.cursor = 'default';
    hideTip(canvas);
}
