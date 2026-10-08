---
tags: [component]
source: Poink.Sim/Components/Charts/XGrid/Display/XGrid.razor
---
# XGrid.razor

Data/pixels split: every row/column/cell value arrives pre-computed via Grid (see XGridStats.Compute); this component turns the active display state into a flat numeric payload and hands it to xGrid.js, which paints the footprint onto one canvas instead of one DOM node per cell.

**Used by:** 23 files (see Backlinks)

## Uses
- [[Components/Charts/XGrid/Display/XGrid.razor.cs.md|XGrid.razor.cs]]
- [[Components/Charts/XGrid/Display/XGridReadoutRow.razor.md|XGridReadoutRow.razor]]
- [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]]
- [[Components/Charts/XGrid/Helper/XGridLogScaleHelper.cs.md|XGridLogScaleHelper.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationRowCache.cs.md|SimulationRowCache.cs]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
