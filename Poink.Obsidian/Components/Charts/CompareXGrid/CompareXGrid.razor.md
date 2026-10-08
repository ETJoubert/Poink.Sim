---
tags: [component, feature/comparexgrid, kind/component]
role: component
feature: CompareXGrid
feature_source: folder
kind: component
code_lines: 66
types: 1
methods: 0
fan_in: 10
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareXGrid/CompareXGrid.razor
---
# CompareXGrid.razor

Several symbols' footprints on one grid of % rows by time columns (see CompareXGridResult), coloured per Mode. The host computes Result and recomputes it when the bound row size or column count changes; mode and difference symbol are display-only. Draws via its own compareXGrid.js module.

**Feature:** CompareXGrid · **Kind:** Razor component · **Code lines:** 66 · **Used by:** 10

## Types
- `CompareXGrid` — component, Razor component · empty

## Uses
- [[Components/Charts/CompareXGrid/CompareXGridModels.cs.md|CompareXGridModels.cs]]
