---
tags: [interface, feature/trader, kind/mixed]
role: interface
feature: Trader
feature_source: folder
kind: mixed
code_lines: 55
types: 2
methods: 5
fan_in: 10
fan_out: 6
summary_source: generated
candidates: []
source: Poink.Sim/Services/Trader/ITrader.cs
---
# ITrader.cs

Contract for the trader.

**Feature:** Trader · **Kind:** mixed types · **Code lines:** 55 · **Used by:** 10

## Types
- `TraderControlMode` — enum, enum · 2 values
- `ITrader` — interface, interface · 5 methods, 15 properties

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/ISimulationBrain.cs.md|ISimulationBrain.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Trader/Orders/PendingOrder.cs.md|PendingOrder.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
