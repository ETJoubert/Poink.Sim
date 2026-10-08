---
tags: [model, feature/bots, kind/data-bundle]
role: model
feature: Bots
feature_source: folder
kind: data-bundle
code_lines: 23
types: 3
methods: 0
fan_in: 4
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/Bots/BotModels.cs
---
# BotModels.cs

One bot and the trading account it owns.

**Feature:** Bots · **Kind:** bundle of data types · **Code lines:** 23 · **Used by:** 4

## Types
- `Bot` — class, data type (at most one helper method) · 3 properties
- `BotTickContext` — record struct, data type (at most one helper method) · 11 properties
- `BotAction` — record, data type (at most one helper method) · 3 properties

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Trader/ITrader.cs.md|ITrader.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
