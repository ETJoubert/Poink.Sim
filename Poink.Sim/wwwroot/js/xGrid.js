// Canvas renderer for the XGrid footprint. The Blazor component (XGrid.razor.cs) owns all
// state and hands over a per-draw payload of pre-computed intensities; this module only
// paints pixels and hit-tests the cursor -- same data/pixels split the component held when
// it rendered to a CSS grid, moved off the DOM so cell count no longer costs DOM nodes.

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
        canvas.__xgrid = { hoverKey: null, mouse: null };
        canvas.addEventListener('mousemove', e => onMove(canvas, dotNetRef, e));
        canvas.addEventListener('mouseleave', () => onLeave(canvas));
    }
    const st = canvas.__xgrid;
    st.payload = payload;
    st.dotNetRef = dotNetRef;

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const W = canvas.clientWidth, H = canvas.clientHeight;
    if (W === 0 || H === 0) return;
    canvas.width = Math.round(W * dpr);
    canvas.height = Math.round(H * dpr);
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    render(ctx, W, H, payload);
}

function render(ctx, W, H, p) {
    ctx.clearRect(0, 0, W, H);
    const rows = p.rows, cols = p.cols;
    if (rows <= 0 || cols <= 0) return;

    const cw = W / cols, ch = H / rows;
    const gap = (cw > 3 && ch > 3) ? 1 : 0;

    ctx.fillStyle = 'rgba(255,255,255,0.03)';
    ctx.fillRect(0, 0, W, H);

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
            ctx.fillRect(c * cw + gap, y + gap, cw - gap, ch - gap);
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
                ctx.fillRect(0, r * ch, W, ch);
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
            hline(ctx, W, y + 0.5);
            hline(ctx, W, y + ch - 0.5);
            ctx.setLineDash([]);
        }
    }

    if (p.current != null) {
        const y = (p.current + 0.5) * ch;
        ctx.fillStyle = cssVar('--color-accent');
        ctx.beginPath();
        ctx.moveTo(W, y - 5);
        ctx.lineTo(W, y + 5);
        ctx.lineTo(W - 6, y);
        ctx.closePath();
        ctx.fill();
    }
}

function hline(ctx, W, y) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(W, y);
    ctx.stroke();
}

function tipEl(canvas) {
    return canvas.parentElement ? canvas.parentElement.querySelector('.xgrid-cell-tip') : null;
}

function onMove(canvas, dotNetRef, e) {
    const st = canvas.__xgrid;
    const p = st.payload;
    if (!p) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left, y = e.clientY - rect.top;
    st.mouse = { x, y };
    const cw = rect.width / p.cols, ch = rect.height / p.rows;
    const col = Math.floor(x / cw), row = Math.floor(y / ch);
    if (col < 0 || row < 0 || col >= p.cols || row >= p.rows) { onLeave(canvas); return; }

    const tip = tipEl(canvas);
    if (!tip) return;
    const key = row + 'x' + col;
    if (st.hoverKey !== key) {
        st.hoverKey = key;
        dotNetRef.invokeMethodAsync('CellTooltip', row, col).then(text => {
            if (!text) { onLeave(canvas); return; }
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

function onLeave(canvas) {
    const st = canvas.__xgrid;
    if (st) st.hoverKey = null;
    const tip = tipEl(canvas);
    if (tip) tip.style.display = 'none';
}
