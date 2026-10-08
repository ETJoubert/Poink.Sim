---
tags: [class, feature/xgrid, kind/mixed]
role: class
feature: XGrid
feature_source: folder
kind: mixed
code_lines: 1004
types: 14
methods: 63
fan_in: 38
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridStats.cs
---
# XGridStats.cs

One row x column intersection of XGrid. Value is the metric XGrid's cell colour is driven by; Count/ UpCount/DownCount ride along so a future replacement metric can be computed without reshaping the grid. Public (not internal) since XGrid.razor.cs's [Parameter] Grid exposes it.

**Feature:** XGrid · **Kind:** mixed types · **Code lines:** 1004 · **Used by:** 38

## Types
- `XGridCell` — record struct, data type (at most one helper method) · 13 properties
- `XGridColumn` — record struct, data type (at most one helper method) · 2 properties
- `XGridLevel` — record struct, data type (at most one helper method) · 2 properties
- `XGridResult` — record struct, data type (at most one helper method) · 34 properties
- `XGridSpectrumMetric` — enum, enum · 4 values
- `XGridHighlightMode` — enum, enum · 2 values
- `XGridPeakMode` — enum, enum · 2 values
- `XGridColumnExtreme` — record struct, data type (at most one helper method) · 3 properties
- `XGridPeakSettings` — record struct, data type (at most one helper method) · 4 properties
- `XGridStats` — class, static helper · 60 methods, 1 field
- `XGridSignalDirection` — enum, enum · 3 values
- `XGridSignalDef` — record, data type (at most one helper method) · 9 properties
- `XGridSignalCatalog` — class, static helper · 3 methods, 2 properties
- `Keys` — class, nested, static constants · 9 fields

## Uses
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
