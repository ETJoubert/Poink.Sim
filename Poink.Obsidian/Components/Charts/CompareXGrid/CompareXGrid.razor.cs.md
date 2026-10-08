---
tags: [code-behind, feature/comparexgrid, kind/component-code]
role: code-behind
feature: CompareXGrid
feature_source: folder
kind: component-code
code_lines: 132
types: 1
methods: 11
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareXGrid/CompareXGrid.razor.cs
---
# CompareXGrid.razor.cs

Several symbols' footprints on one grid of % rows by time columns (see CompareXGridResult), coloured per Mode. The host computes Result and recomputes it when the bound row size or column count changes; mode and difference symbol are display-only. Draws via its own compareXGrid.js module.

**Feature:** CompareXGrid · **Kind:** component code-behind · **Code lines:** 132 · **Used by:** 0

## Types
- `CompareXGrid` — class, Razor component · 11 methods, 22 properties, 7 fields

## Uses
- [[Components/Charts/CompareXGrid/CompareXGrid.razor.md|CompareXGrid.razor]]
- [[Components/Charts/CompareXGrid/CompareXGridModels.cs.md|CompareXGridModels.cs]]
- [[wwwroot/js/compareXGrid.js.md|compareXGrid.js]]
