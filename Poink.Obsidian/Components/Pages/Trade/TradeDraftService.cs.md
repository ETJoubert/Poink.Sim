---
tags: [service]
source: Poink.Sim/Components/Pages/Trade/TradeDraftService.cs
---
# TradeDraftService.cs

Scoped, so it's shared between TradePage (writer, on TradeConsole's behalf) and TradeChartPanel (reader) for one circuit without either being a parent of the other -- see ITradeCoordinator for the same pattern applied to the Trade page's other cross-panel state.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Components/Pages/Trade/ITradeDraftService.cs.md|ITradeDraftService.cs]]
- [[Components/Pages/Trade/TradeDraftModels.cs.md|TradeDraftModels.cs]]
