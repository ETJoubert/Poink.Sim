---
tags: [interface, feature/trade, kind/interface]
role: interface
feature: Trade
feature_source: folder
kind: interface
code_lines: 13
types: 1
methods: 4
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Services/IPositionCloseService.cs
---
# IPositionCloseService.cs

The quick console's close helpers. Armed sides close each position on its own once price is back to its entry or better; a side disarms itself once it has no positions left or a close fails, and every side disarms when the selected account changes.

**Feature:** Trade · **Kind:** interface · **Code lines:** 13 · **Used by:** 3

## Types
- `IPositionCloseService` — interface, interface · 4 methods, 1 property, 2 events

## Uses
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
