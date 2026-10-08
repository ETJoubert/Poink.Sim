---
tags: [interface, feature/trade, kind/interface]
role: interface
feature: Trade
feature_source: folder
kind: interface
code_lines: 13
types: 1
methods: 6
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Services/ITradeBarFeed.cs
---
# ITradeBarFeed.cs

Live bars for every symbol the Trade page charts or compares: each is loaded once through the database-first tick history, then kept current from the session's tick stream.

**Feature:** Trade · **Kind:** interface · **Code lines:** 13 · **Used by:** 4

## Types
- `ITradeBarFeed` — interface, interface · 6 methods, 2 events

## Uses
- [[Components/Pages/Trade/Services/TradeSymbolSeries.cs.md|TradeSymbolSeries.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
