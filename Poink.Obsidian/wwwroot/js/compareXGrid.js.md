---
tags: [script, feature/comparexgrid, kind/script]
role: script
feature: CompareXGrid
feature_source: usage
kind: script
code_lines: 251
types: 0
methods: 0
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/wwwroot/js/compareXGrid.js
---
# compareXGrid.js

Canvas renderer for CompareXGrid.razor.cs: % rows by time columns, each cell coloured from every symbol's share of its own busiest cell -- 'dominant' takes the highest share's colour, 'difference' shades towards whichever of the first symbol and the chosen one has the higher share.

**Feature:** CompareXGrid (by usage) · **Kind:** script · **Code lines:** 251 · **Used by:** 1

## Uses
- [[wwwroot/js/chartColors.js.md|chartColors.js]]
- [[wwwroot/js/chartTooltip.js.md|chartTooltip.js]]
