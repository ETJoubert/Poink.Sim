// Canvas renderer for one NEAT genome's topology. The Blazor component
// (NeatTopologyGraph.razor.cs) owns all layout/color logic and hands over a per-draw payload
// of pre-computed node/edge positions (0-100% coordinates) and color tokens; this module only
// paints pixels. Same split as xGrid.js.

function cssVar(name, el = document.documentElement) {
    return getComputedStyle(el).getPropertyValue(name).trim();
}

export function draw(canvas, payload) {
    if (!canvas) return;

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const cssWidth = canvas.clientWidth;
    const cssHeight = canvas.clientHeight;
    const w = Math.round(cssWidth * dpr);
    const h = Math.round(cssHeight * dpr);
    if (canvas.width !== w || canvas.height !== h) {
        canvas.width = w;
        canvas.height = h;
    }

    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, cssWidth, cssHeight);

    ctx.lineCap = 'round';
    for (const e of payload.edges) {
        ctx.beginPath();
        ctx.moveTo(e.x1 / 100 * cssWidth, e.y1 / 100 * cssHeight);
        ctx.lineTo(e.x2 / 100 * cssWidth, e.y2 / 100 * cssHeight);
        ctx.strokeStyle = cssVar(e.color);
        ctx.globalAlpha = e.opacity;
        ctx.setLineDash(e.enabled ? [] : [2, 2]);
        ctx.lineWidth = e.enabled ? 0.6 : 0.4;
        ctx.stroke();
    }
    ctx.globalAlpha = 1;
    ctx.setLineDash([]);

    const borderColor = cssVar('--color-bg-terminal');
    for (const n of payload.nodes) {
        const cx = n.x / 100 * cssWidth;
        const cy = n.y / 100 * cssHeight;
        const r = n.d / 2;
        const fill = cssVar(n.color);

        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fillStyle = fill;
        ctx.shadowColor = fill;
        ctx.shadowBlur = 3;
        ctx.fill();
        ctx.shadowBlur = 0;
        ctx.lineWidth = 0.75;
        ctx.strokeStyle = borderColor;
        ctx.stroke();
    }
}
