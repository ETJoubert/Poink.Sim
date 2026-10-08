---
tags: [service, feature/trade, kind/behavior]
role: service
feature: Trade
feature_source: folder
kind: behavior
code_lines: 15
types: 1
methods: 2
fan_in: 1
fan_out: 3
summary_source: interface-doc
candidates: []
source: Poink.Sim/Components/Pages/Trade/TradeDraftService.cs
---
# TradeDraftService.cs

Scoped, so it's shared between TradePage (writer, on TradeConsole's behalf) and TradeChartPanel (reader) for one circuit without either being a parent of the other -- see ITradeCoordinator for the same pattern applied to the Trade page's other cross-panel state.

**Feature:** Trade · **Kind:** class with behaviour · **Code lines:** 15 · **Used by:** 1

## Types
- `TradeDraftService` — class, class with behaviour · 2 methods, 1 property, 1 event

## Uses
- [[Components/Pages/Trade/ITradeDraftService.cs.md|ITradeDraftService.cs]]
- [[Components/Pages/Trade/TradeDraftModels.cs.md|TradeDraftModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
