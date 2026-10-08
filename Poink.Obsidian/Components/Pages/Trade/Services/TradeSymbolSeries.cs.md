---
tags: [class, feature/trade, kind/behavior]
role: class
feature: Trade
feature_source: folder
kind: behavior
code_lines: 110
types: 1
methods: 10
fan_in: 4
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Services/TradeSymbolSeries.cs
---
# TradeSymbolSeries.cs

One symbol's bars at one bar size: aggregated from its loaded history, then advanced by live ticks. Each update swaps in new lists rather than mutating, so a render holding the old ones never sees a half-applied tick and reference comparisons tell when bars changed.

**Feature:** Trade · **Kind:** class with behaviour · **Code lines:** 110 · **Used by:** 4

## Types
- `TradeSymbolSeries` — class, class with behaviour · 10 methods, 9 properties, 3 fields, 1 constructor

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
