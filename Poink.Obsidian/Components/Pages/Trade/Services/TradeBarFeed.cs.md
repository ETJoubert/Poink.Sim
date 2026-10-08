---
tags: [class, feature/trade, kind/behavior]
role: class
feature: Trade
feature_source: folder
kind: behavior
code_lines: 79
types: 1
methods: 11
fan_in: 1
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Services/TradeBarFeed.cs
---
# TradeBarFeed.cs

Scoped per circuit, so the Trade page and a popped-out chart each hold their own series. Ticks that arrive while a symbol's history is still loading are not applied, since the aggregator must see ticks in time order. Series belong to the account they were loaded on: a switch (e.g.

**Feature:** Trade · **Kind:** class with behaviour · **Code lines:** 79 · **Used by:** 1

## Types
- `TradeBarFeed` — class, class with behaviour · 11 methods, 8 fields, 1 constructor, 2 events

## Uses
- [[Components/Pages/Trade/Services/ITradeBarFeed.cs.md|ITradeBarFeed.cs]]
- [[Components/Pages/Trade/Services/TradeSymbolSeries.cs.md|TradeSymbolSeries.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/MarketData/History/ITickHistoryService.cs.md|ITickHistoryService.cs]]
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
