---
tags: [class, feature/trade, kind/behavior]
role: class
feature: Trade
feature_source: folder
kind: behavior
code_lines: 44
types: 2
methods: 4
fan_in: 2
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Services/TradeLiveTickSource.cs
---
# TradeLiveTickSource.cs

Feeds the Trade compare TxFold tick by tick: each symbol's held series supplies every tick from its first one on (loaded history plus live ticks not yet written), and only the older part of a window is read from the tick tables -- once, then cached, since the window's start only moves forward as the anchor follows the…

**Feature:** Trade · **Kind:** class with behaviour · **Code lines:** 44 · **Used by:** 2

## Types
- `TradeLiveTickSource` — class, class with behaviour · 4 methods, 3 fields, 1 constructor
- `StoredBlock` — record, nested, data type (at most one helper method) · 3 properties

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Components/Charts/CompareTxFold/ICompareTxFoldTickSource.cs.md|ICompareTxFoldTickSource.cs]]
- [[Components/Pages/Trade/Services/ITradeBarFeed.cs.md|ITradeBarFeed.cs]]
- [[Helpers/SplitTickReadHelper.cs.md|SplitTickReadHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
