// Stylized, ambient "network is thinking" canvas animation for the Train
// page's Live tab -- NOT a literal introspection of MlpPolicyBrain's real
// activations (nothing pipes per-tick internals out of the brain today), just
// a visual cue that flips between an idle ambient state and a busier
// pulsing/flowing state depending on whether training is running. Node/layer
// counts below are stylized down from the real 24/32/16/4 for legibility, not
// a literal architecture diagram.
//
// Colors are read from CSS custom properties at draw time (cssVar helper,
// same convention as Poink.Shared.Client's CandlestickChart.ts) so this
// repaints correctly if the theme changes, rather than hardcoding hex values.

const LAYER_SIZES = [8, 6, 5, 4];
const OUTPUT_LABELS = ['HOLD', 'LONG', 'SHORT', 'CLOSE'];

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

function layerPositions(width, height) {
    const layerGap = width / (LAYER_SIZES.length + 1);
    return LAYER_SIZES.map((count, layerIndex) => {
        const x = layerGap * (layerIndex + 1);
        const nodeGap = height / (count + 1);
        const nodes = [];
        for (let i = 0; i < count; i++) {
            nodes.push({ x, y: nodeGap * (i + 1) });
        }
        return nodes;
    });
}

export function create(canvas) {
    const ctx = canvas.getContext('2d');
    let running = false;
    let animationId = null;
    let dpr = window.devicePixelRatio || 1;
    let t = 0;

    function resize() {
        const rect = canvas.getBoundingClientRect();
        dpr = window.devicePixelRatio || 1;
        canvas.width = Math.max(1, rect.width * dpr);
        canvas.height = Math.max(1, rect.height * dpr);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    function draw() {
        const width = canvas.width / dpr;
        const height = canvas.height / dpr;
        ctx.clearRect(0, 0, width, height);

        const primary = cssVar('--color-primary', '#00ff9d');
        const secondary = cssVar('--color-secondary', '#00d4ff');
        const connectionColor = 'rgba(255, 255, 255, 0.12)';

        const positions = layerPositions(width, height);

        // Connections, with a traveling pulse per edge while running.
        for (let l = 0; l < positions.length - 1; l++) {
            const from = positions[l];
            const to = positions[l + 1];
            from.forEach((a, ai) => {
                to.forEach((b, bi) => {
                    ctx.beginPath();
                    ctx.moveTo(a.x, a.y);
                    ctx.lineTo(b.x, b.y);
                    ctx.strokeStyle = connectionColor;
                    ctx.lineWidth = 1;
                    ctx.stroke();

                    if (running) {
                        const phase = (t * 0.6 - l * 0.9 - (ai + bi) * 0.03) % 3;
                        if (phase > 0 && phase < 1) {
                            const px = a.x + (b.x - a.x) * phase;
                            const py = a.y + (b.y - a.y) * phase;
                            ctx.beginPath();
                            ctx.arc(px, py, 2, 0, Math.PI * 2);
                            ctx.fillStyle = secondary;
                            ctx.shadowColor = secondary;
                            ctx.shadowBlur = 6;
                            ctx.fill();
                            ctx.shadowBlur = 0;
                        }
                    }
                });
            });
        }

        // Nodes -- glow wave left-to-right while running, dim/static while idle.
        positions.forEach((nodes, layerIndex) => {
            nodes.forEach((n, ni) => {
                const glowPhase = running ? (Math.sin(t * 2 - layerIndex * 0.8 - ni * 0.2) + 1) / 2 : 0;
                ctx.beginPath();
                ctx.arc(n.x, n.y, 5, 0, Math.PI * 2);
                ctx.globalAlpha = running ? 0.35 + glowPhase * 0.65 : 0.25;
                ctx.fillStyle = primary;
                ctx.shadowColor = primary;
                ctx.shadowBlur = running ? 8 * glowPhase : 0;
                ctx.fill();
                ctx.shadowBlur = 0;
                ctx.globalAlpha = 1;

                if (layerIndex === positions.length - 1) {
                    ctx.fillStyle = secondary;
                    ctx.font = '10px "Cascadia Mono", Consolas, monospace';
                    ctx.textAlign = 'left';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(OUTPUT_LABELS[ni] ?? '', n.x + 10, n.y);
                }
            });
        });

        t += 0.02;
        animationId = requestAnimationFrame(draw);
    }

    resize();
    const resizeHandler = () => resize();
    window.addEventListener('resize', resizeHandler);
    animationId = requestAnimationFrame(draw);

    return {
        setRunning(value) {
            running = value;
        },
        dispose() {
            if (animationId !== null) cancelAnimationFrame(animationId);
            window.removeEventListener('resize', resizeHandler);
        },
    };
}
