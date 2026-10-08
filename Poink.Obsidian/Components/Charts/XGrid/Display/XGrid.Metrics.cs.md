---
tags: [class, feature/xgrid, kind/component-code]
role: class
feature: XGrid
feature_source: folder
kind: component-code
code_lines: 99
types: 1
methods: 21
fan_in: 0
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Display/XGrid.Metrics.cs
---
# XGrid.Metrics.cs

XGrid's overlay and spectrum-highlight controls, rendered in its own header rows. Each setting stays a [Parameter] + *Changed callback so XGridController keeps owning the state it also merges with Indicators.

**Feature:** XGrid · **Kind:** component code-behind · **Code lines:** 99 · **Used by:** 0

## Types
- `XGrid` — class, class with behaviour · 21 methods, 7 properties

## Uses
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridDisplaySettings.cs.md|XGridDisplaySettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]]
- [[Components/Charts/XGrid/Helper/XGridPeakOverlaySettings.cs.md|XGridPeakOverlaySettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridSpectrumSettings.cs.md|XGridSpectrumSettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
