---
tags: [class]
source: Poink.Sim/Services/Symbols/SymbolCategoryClassifier.cs
---
# SymbolCategoryClassifier.cs

Maps a cTrader symbol category's name (plus its asset class's name) to one of the Trade page's filter buckets. Broker category names aren't standardized, so these are a first-draft guess -- check them against SymbolService.RefreshCategoriesFromCTraderAsync's log output and adjust here.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
