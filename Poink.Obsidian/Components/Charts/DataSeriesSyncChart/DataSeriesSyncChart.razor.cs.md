---
tags: [code-behind, feature/dataseriessyncchart, kind/component-code]
role: code-behind
feature: DataSeriesSyncChart
feature_source: folder
kind: component-code
code_lines: 47
types: 1
methods: 2
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/DataSeriesSyncChart/DataSeriesSyncChart.razor.cs
---
# DataSeriesSyncChart.razor.cs

Left-to-right day squares from the tick store's latest coverage to today, via IDataSeriesSyncService -- the leftmost square is the latest day already in the database, the rightmost is today, and everything between is either found in the registry's saved import source directory (ready to import) or unverified (would…

**Feature:** DataSeriesSyncChart · **Kind:** component code-behind · **Code lines:** 47 · **Used by:** 0

## Types
- `DataSeriesSyncChart` — class, Razor component · 2 methods, 8 properties, 3 fields

## Uses
- [[Components/Charts/DataSeriesSyncChart/DataSeriesSyncChart.razor.md|DataSeriesSyncChart.razor]]
- [[Services/DataSeriesSync/DataSeriesSyncModels.cs.md|DataSeriesSyncModels.cs]]
- [[Services/DataSeriesSync/IDataSeriesSyncService.cs.md|IDataSeriesSyncService.cs]]
