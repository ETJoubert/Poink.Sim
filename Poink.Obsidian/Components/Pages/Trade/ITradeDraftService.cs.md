---
tags: [interface, feature/trade, kind/interface]
role: interface
feature: Trade
feature_source: folder
kind: interface
code_lines: 7
types: 1
methods: 2
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/ITradeDraftService.cs
---
# ITradeDraftService.cs

Scoped, so it's shared between TradePage (writer, on TradeConsole's behalf) and TradeChartPanel (reader) for one circuit without either being a parent of the other -- see ITradeCoordinator for the same pattern applied to the Trade page's other cross-panel state.

**Feature:** Trade · **Kind:** interface · **Code lines:** 7 · **Used by:** 4

## Types
- `ITradeDraftService` — interface, interface · 2 methods, 1 property, 1 event

## Uses
- [[Components/Pages/Trade/TradeDraftModels.cs.md|TradeDraftModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
