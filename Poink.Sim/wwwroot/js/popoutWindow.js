// Binds a plain DOM click listener directly on the button (bypassing Blazor Server's
// @onclick round-trip) so window.open still runs inside the browser's synchronous
// user-gesture stack -- routing it through the server first is what gets these blocked
// by popup blockers. The button's data-popout-* attributes are read fresh on every click,
// so Blazor's normal re-render keeps them current without any re-bind.
export function bind(button) {
    if (button.dataset.popoutBound) return;
    button.dataset.popoutBound = '1';

    button.addEventListener('click', () => {
        const url = button.dataset.popoutUrl;
        if (!url) return;

        const name = button.dataset.popoutName || '';
        const width = parseInt(button.dataset.popoutWidth, 10) || 900;
        const height = parseInt(button.dataset.popoutHeight, 10) || 640;
        const features = `popup=yes,width=${width},height=${height}`;

        const win = window.open(url, name, features);
        if (win) win.focus();
    });
}
