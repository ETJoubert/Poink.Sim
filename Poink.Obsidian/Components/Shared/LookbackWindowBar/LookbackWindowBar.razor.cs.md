---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 63
types: 1
methods: 4
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/LookbackWindowBar/LookbackWindowBar.razor.cs
---
# LookbackWindowBar.razor.cs

Bar/tick lookback window control -- a rail whose highlighted segment can be dragged by its right edge (resizes, anchoring the far edge) or by its body (pans, width unchanged), plus plain number inputs for both values. Shared by SimulationPage and TradeChartPanel.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 63 · **Used by:** 0

## Types
- `LookbackWindowBar` — class, Razor component · 4 methods, 14 properties, 3 fields

## Uses
- [[Components/Shared/LookbackWindowBar/LookbackWindowBar.razor.md|LookbackWindowBar.razor]]
- [[wwwroot/js/lookbackWindowBar.js.md|lookbackWindowBar.js]]
