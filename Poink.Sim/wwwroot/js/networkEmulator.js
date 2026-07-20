// Stylized, ambient "network is thinking" canvas animation for the Train
// page's Live tab -- NOT a literal introspection of a brain's real
// activations (nothing pipes per-tick internals out mid-decision), just a
// visual cue that flips between an idle ambient state and a busier
// pulsing/flowing state depending on whether training is running.
//
// Layer shapes ARE the real selected brain's architecture though --
// setLayerShapes(shapes) below is called from NetworkEmulator.razor.cs with
// IPolicyGradientBrain.DescribeNetworkShape's output (a list of {units,
// isRecurrent}), so an LSTM brain draws a visibly different, bigger diagram
// than the MLP one, with no brain-specific code here. Node-per-layer counts
// are still clamped/sqrt-scaled down for legibility (see nodeCount) rather
// than drawn one-dot-per-unit -- a 64-unit LSTM hidden layer would otherwise
// be unreadable spaghetti. Each layer gets a role label with its real (not
// aggregated-down) unit count underneath (see layerLabels), and the output
// layer's individual nodes additionally get their action names.
//
// Colors are read from CSS custom properties at draw time (cssVar helper,
// same convention as Poink.Shared.Client's CandlestickChart.ts) so this
// repaints correctly if the theme changes, rather than hardcoding hex values.

const OUTPUT_LABELS = ['HOLD', 'LONG', 'SHORT', 'CLOSE'];

// Shown until the first real setLayerShapes call arrives -- MlpPolicyBrain's
// actual shape, not an arbitrary placeholder.
const DEFAULT_LAYERS = [
    { units: 24, recurrent: false }, { units: 32, recurrent: false },
    { units: 16, recurrent: false }, { units: 4, recurrent: false },
];

function cssVar(name, fallback) {
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

// Legibility clamp, not a literal unit count -- sqrt so a 64-unit LSTM hidden
// layer (8 dots) still reads as visibly "bigger" than a 4-unit output layer
// (3 dots, the floor) without needing 64 actual dots.
function nodeCount(units) {
    return Math.max(3, Math.min(8, Math.round(Math.sqrt(Math.max(1, units)))));
}

function layerPositions(width, height, layers) {
    const layerGap = width / (layers.length + 1);
    return layers.map((layer, layerIndex) => {
        const x = layerGap * (layerIndex + 1);
        const count = nodeCount(layer.units);
        const nodeGap = height / (count + 1);
        const nodes = [];
        for (let i = 0; i < count; i++) {
            nodes.push({ x, y: nodeGap * (i + 1) });
        }
        return nodes;
    });
}

// One label per layer, e.g. "INPUT (24)" / "HIDDEN 1 (32)" / "RECURRENT (64)"
// -- role from position + the recurrent flag, real unit count in parens since
// the drawn dot count is a legibility aggregate (see nodeCount), not the
// literal size. "RECURRENT" rather than "LSTM" so a future GRU-based brain
// (also IsRecurrent) doesn't get mislabeled.
function layerLabels(layers) {
    const total = layers.length;
    const hiddenTotal = layers.filter((l, i) => !l.recurrent && i !== 0 && i !== total - 1).length;
    let hiddenSeen = 0;
    return layers.map((layer, index) => {
        if (layer.recurrent) return `RECURRENT (${layer.units})`;
        if (index === 0) return `INPUT (${layer.units})`;
        if (index === total - 1) return `OUTPUT (${layer.units})`;
        hiddenSeen += 1;
        return hiddenTotal > 1 ? `HIDDEN ${hiddenSeen} (${layer.units})` : `HIDDEN (${layer.units})`;
    });
}

export function create(canvas) {
    const ctx = canvas.getContext('2d');
    let running = false;
    let layers = DEFAULT_LAYERS;
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
        const muted = cssVar('--color-muted', 'rgba(255, 255, 255, 0.45)');
        const connectionColor = 'rgba(255, 255, 255, 0.12)';

        const positions = layerPositions(width, height, layers);
        const labels = layerLabels(layers);

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

        // Recurrent-layer loop-back arc -- drawn behind its nodes, the one
        // deliberate deviation from "purely feedforward left-to-right" that
        // marks a layer as looping over a sequence (an LSTM) rather than
        // being a plain feedforward one.
        positions.forEach((nodes, layerIndex) => {
            if (!layers[layerIndex]?.recurrent || nodes.length === 0) return;
            const top = nodes[0];
            const bottom = nodes[nodes.length - 1];
            const bulge = 16 + (running ? Math.sin(t * 1.5) * 4 : 0);
            ctx.beginPath();
            ctx.moveTo(top.x, top.y);
            ctx.bezierCurveTo(top.x - bulge, top.y, top.x - bulge, bottom.y, bottom.x, bottom.y);
            ctx.setLineDash([3, 4]);
            ctx.strokeStyle = secondary;
            ctx.globalAlpha = running ? 0.5 : 0.25;
            ctx.lineWidth = 1;
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.globalAlpha = 1;
        });

        // Nodes -- glow wave left-to-right while running, dim/static while idle.
        positions.forEach((nodes, layerIndex) => {
            nodes.forEach((n, ni) => {
                const glowPhase = running ? (Math.sin(t * 2 - layerIndex * 0.8 - ni * 0.2) + 1) / 2 : 0;
                ctx.beginPath();
                ctx.arc(n.x, n.y, 5, 0, Math.PI * 2);
                ctx.globalAlpha = running ? 0.35 + glowPhase * 0.65 : 0.25;
                ctx.fillStyle = layers[layerIndex]?.recurrent ? secondary : primary;
                ctx.shadowColor = ctx.fillStyle;
                ctx.shadowBlur = running ? 8 * glowPhase : 0;
                ctx.fill();
                ctx.shadowBlur = 0;
                ctx.globalAlpha = 1;

                // Only the true output layer gets action labels -- guarded by
                // node count too, since a stylized-down layer might not have
                // exactly OUTPUT_LABELS.length dots (e.g. a wide hidden layer
                // clamped down to 8 shouldn't accidentally get labeled).
                if (layerIndex === positions.length - 1 && nodes.length === OUTPUT_LABELS.length) {
                    ctx.fillStyle = secondary;
                    ctx.font = '10px "Cascadia Mono", Consolas, monospace';
                    ctx.textAlign = 'left';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(OUTPUT_LABELS[ni] ?? '', n.x + 10, n.y);
                }
            });
        });

        // Per-layer role label (INPUT/HIDDEN n/RECURRENT/OUTPUT + real unit
        // count), centered under each column -- individual input nodes don't
        // get their own labels since 24 real inputs are aggregated down to a
        // handful of dots (see nodeCount) with no 1:1 meaning left to label.
        positions.forEach((nodes, layerIndex) => {
            if (nodes.length === 0) return;
            const bottom = nodes[nodes.length - 1];
            ctx.fillStyle = layers[layerIndex]?.recurrent ? secondary : muted;
            ctx.font = '9px "Cascadia Mono", Consolas, monospace';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            ctx.fillText(labels[layerIndex] ?? '', bottom.x, bottom.y + 12);
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
        // shapes is IPolicyGradientBrain.DescribeNetworkShape's output as
        // serialized by Blazor JSInterop (camelCase by default, but read both
        // casings defensively rather than assume). Ignored if empty --
        // e.g. a non-neural brain is selected -- so the diagram keeps
        // showing the last real shape instead of collapsing to nothing.
        setLayerShapes(shapes) {
            if (!Array.isArray(shapes) || shapes.length === 0) return;
            layers = shapes.map(s => ({
                units: s.units ?? s.Units ?? 1,
                recurrent: s.isRecurrent ?? s.IsRecurrent ?? false,
            }));
        },
        dispose() {
            if (animationId !== null) cancelAnimationFrame(animationId);
            window.removeEventListener('resize', resizeHandler);
        },
    };
}
