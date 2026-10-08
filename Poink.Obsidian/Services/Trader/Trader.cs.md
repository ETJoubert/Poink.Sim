---
tags: [class, feature/trader, kind/behavior]
role: class
feature: Trader
feature_source: folder
kind: behavior
code_lines: 235
types: 1
methods: 11
fan_in: 55
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/Trader/Trader.cs
---
# Trader.cs

Executes trading decisions against one account. Advances stop loss, take profit, margin stop-out and pending orders on every tick, and charges commission on open and close.

**Feature:** Trader · **Kind:** class with behaviour · **Code lines:** 235 · **Used by:** 55

## Types
- `Trader` — class, class with behaviour · 11 methods, 15 properties, 7 fields, 1 constructor

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/ISimulationBrain.cs.md|ISimulationBrain.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Trader/ITrader.cs.md|ITrader.cs]]
- [[Services/Trader/Orders/PendingOrder.cs.md|PendingOrder.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
