---
tags: [script, feature/shared, kind/script]
role: script
feature: Shared
feature_source: shared
kind: script
code_lines: 13
types: 0
methods: 0
fan_in: 1
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/wwwroot/js/popoutWindow.js
---
# popoutWindow.js

Binds a plain DOM click listener directly on the button (bypassing Blazor Server's @onclick round-trip) so window.open still runs inside the browser's synchronous user-gesture stack -- routing it through the server first is what gets these blocked by popup blockers.

**Feature:** Shared · **Kind:** script · **Code lines:** 13 · **Used by:** 1

## Uses
- _nothing in Poink.Sim_
