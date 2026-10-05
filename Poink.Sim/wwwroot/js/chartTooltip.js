// Tooltip placement shared by the compare chart modules.

const OFFSET = 12;
const MARGIN = 8;

// Places the tooltip beside the cursor, flipping to the other side when it would run past the container's right or
// bottom edge, then clamps it inside -- the panels clip their contents, so an overflowing tooltip would be cut off.
export function placeTooltip(tooltip, container, x, y) {
    const width = container?.clientWidth ?? 0;
    const height = container?.clientHeight ?? 0;
    const tipWidth = tooltip.offsetWidth;
    const tipHeight = tooltip.offsetHeight;

    let left = x + OFFSET;
    if (left + tipWidth > width - MARGIN) left = x - OFFSET - tipWidth;
    let top = y + OFFSET;
    if (top + tipHeight > height - MARGIN) top = y - OFFSET - tipHeight;

    tooltip.style.left = `${Math.max(MARGIN, Math.min(left, width - tipWidth - MARGIN))}px`;
    tooltip.style.top = `${Math.max(MARGIN, Math.min(top, height - tipHeight - MARGIN))}px`;
}
