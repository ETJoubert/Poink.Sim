---
tags: [script, feature/shared, kind/script]
role: script
feature: Shared
feature_source: shared
kind: script
code_lines: 93
types: 0
methods: 0
fan_in: 1
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/wwwroot/js/lookbackWindowBar.js
---
# lookbackWindowBar.js

Draggable "LOOKBACK WINDOW" rail shared by SimulationPage and TradeChartPanel -- the highlighted segment (`.lkbk-fill`) is a plain DOM element, not canvas, and its geometry comes straight from Width/Offset/MaxWindow (kept on the rail's dataset by Blazor on every render) rather than the bar-indexed math…

**Feature:** Shared · **Kind:** script · **Code lines:** 93 · **Used by:** 1

## Uses
- _nothing in Poink.Sim_
