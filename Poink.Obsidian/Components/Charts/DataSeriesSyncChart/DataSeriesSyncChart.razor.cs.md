---
tags: [code-behind]
source: Poink.Sim/Components/Charts/DataSeriesSyncChart/DataSeriesSyncChart.razor.cs
---
# DataSeriesSyncChart.razor.cs

Left-to-right day squares from the tick store's latest coverage to today, via IDataSeriesSyncService -- the leftmost square is the latest day already in the database, the rightmost is today, and everything between is either found in the registry's saved import source directory (ready to import) or unverified (would…

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Charts/DataSeriesSyncChart/DataSeriesSyncChart.razor.md|DataSeriesSyncChart.razor]]
- [[Services/DataSeriesSync/DataSeriesSyncModels.cs.md|DataSeriesSyncModels.cs]]
- [[Services/DataSeriesSync/IDataSeriesSyncService.cs.md|IDataSeriesSyncService.cs]]
