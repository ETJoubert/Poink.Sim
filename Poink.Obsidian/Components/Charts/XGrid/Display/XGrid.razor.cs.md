---
tags: [code-behind]
source: Poink.Sim/Components/Charts/XGrid/Display/XGrid.razor.cs
---
# XGrid.razor.cs

Data/pixels split: every row/column/cell value arrives pre-computed via Grid (see XGridStats.Compute); this component turns the active display state into a flat numeric payload and hands it to xGrid.js, which paints the footprint onto one canvas instead of one DOM node per cell.

**Used by:** 1 file (see Backlinks)

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
- [[wwwroot/js/xGrid.js.md|xGrid.js]]
