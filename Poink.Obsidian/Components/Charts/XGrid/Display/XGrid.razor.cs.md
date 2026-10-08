---
tags: [code-behind, feature/xgrid, kind/component-code]
role: code-behind
feature: XGrid
feature_source: folder
kind: component-code
code_lines: 809
types: 5
methods: 79
fan_in: 1
fan_out: 20
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Display/XGrid.razor.cs
---
# XGrid.razor.cs

Data/pixels split: every row/column/cell value arrives pre-computed via Grid (see XGridStats.Compute); this component turns the active display state into a flat numeric payload and hands it to xGrid.js, which paints the footprint onto one canvas instead of one DOM node per cell.

**Feature:** XGrid · **Kind:** component code-behind · **Code lines:** 809 · **Used by:** 1

## Types
- `XGrid` — class, Razor component · 79 methods, 26 properties, 20 fields
- `OrderDraft` — class, nested, data type (at most one helper method) · 5 properties
- `OrderPanelMode` — enum, nested, enum · 3 values
- `OrderArm` — enum, nested, enum · 3 values
- `SegmentMeasure` — record struct, nested, data type (at most one helper method) · 4 properties

## Uses
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Display/XGridExternalDraft.cs.md|XGridExternalDraft.cs]]
- [[Components/Charts/XGrid/Display/XGridLine.cs.md|XGridLine.cs]]
- [[Components/Charts/XGrid/Display/XGridOrder.cs.md|XGridOrder.cs]]
- [[Components/Charts/XGrid/Display/XGridOrderModification.cs.md|XGridOrderModification.cs]]
- [[Components/Charts/XGrid/Display/XGridPending.cs.md|XGridPending.cs]]
- [[Components/Charts/XGrid/Display/XGridPosition.cs.md|XGridPosition.cs]]
- [[Components/Charts/XGrid/Display/XGridReadout.cs.md|XGridReadout.cs]]
- [[Components/Charts/XGrid/Display/XGridTooltipRow.cs.md|XGridTooltipRow.cs]]
- [[Components/Charts/XGrid/Helper/XGridDisplaySettings.cs.md|XGridDisplaySettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridLogScaleHelper.cs.md|XGridLogScaleHelper.cs]]
- [[Components/Charts/XGrid/Helper/XGridPeakOverlaySettings.cs.md|XGridPeakOverlaySettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridSelection.cs.md|XGridSelection.cs]]
- [[Components/Charts/XGrid/Helper/XGridSpectrumSettings.cs.md|XGridSpectrumSettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Charts/XGrid/Helper/XGridSweepMode.cs.md|XGridSweepMode.cs]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[wwwroot/js/xGrid.js.md|xGrid.js]]
