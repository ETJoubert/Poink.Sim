// Theme colour helpers shared by the compare chart modules.

export function cssColor(element, varName) {
    return getComputedStyle(element).getPropertyValue(varName).trim() || '#c9c9c9';
}

// Canvas needs a concrete colour, so theme hex/rgb values are rewritten as rgba rather than layered with CSS opacity.
export function withAlpha(color, alpha) {
    const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(color);
    if (hex) {
        const digits = hex[1].length === 3 ? [...hex[1]].map(d => d + d).join('') : hex[1];
        const [r, g, b] = [0, 2, 4].map(i => parseInt(digits.slice(i, i + 2), 16));
        return `rgba(${r},${g},${b},${alpha})`;
    }
    const rgb = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)/i.exec(color);
    return rgb ? `rgba(${rgb[1]},${rgb[2]},${rgb[3]},${alpha})` : color;
}
