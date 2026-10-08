---
tags: [service, feature/trade, kind/behavior]
role: service
feature: Trade
feature_source: folder
kind: behavior
code_lines: 195
types: 1
methods: 13
fan_in: 1
fan_out: 9
summary_source: interface-doc
candidates: []
source: Poink.Sim/Components/Pages/Trade/Services/PositionCloseService.cs
---
# PositionCloseService.cs

The quick console's close helpers. Armed sides close each position on its own once price is back to its entry or better; a side disarms itself once it has no positions left or a close fails, and every side disarms when the selected account changes.

**Feature:** Trade · **Kind:** class with behaviour · **Code lines:** 195 · **Used by:** 1

## Types
- `PositionCloseService` — class, class with behaviour · 13 methods, 1 property, 13 fields, 1 constructor, 2 events

## Uses
- [[Components/Pages/Trade/Helpers/PositionCloseHelper.cs.md|PositionCloseHelper.cs]]
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Components/Pages/Trade/Services/IPositionCloseService.cs.md|IPositionCloseService.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/CTrader/ITradeExecutionAccess.cs.md|ITradeExecutionAccess.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
