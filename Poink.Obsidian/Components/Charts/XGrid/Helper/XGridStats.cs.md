---
tags: [class]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridStats.cs
---
# XGridStats.cs

One row x column intersection of XGrid. Value is the metric XGrid's cell colour is driven by; Count/ UpCount/DownCount ride along so a future replacement metric can be computed without reshaping the grid. Public (not internal) since XGrid.razor.cs's [Parameter] Grid exposes it.

**Used by:** 38 files (see Backlinks)

## Uses
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
