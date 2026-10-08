---
tags: [script]
source: Poink.Sim/wwwroot/js/popoutWindow.js
---
# popoutWindow.js

Binds a plain DOM click listener directly on the button (bypassing Blazor Server's @onclick round-trip) so window.open still runs inside the browser's synchronous user-gesture stack -- routing it through the server first is what gets these blocked by popup blockers.

**Used by:** 1 file (see Backlinks)

## Uses
- _nothing in Poink.Sim_
