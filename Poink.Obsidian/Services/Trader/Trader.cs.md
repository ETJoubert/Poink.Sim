---
tags: [class]
source: Poink.Sim/Services/Trader/Trader.cs
---
# Trader.cs

Executes trading decisions against one account. Advances stop loss, take profit, margin stop-out and pending orders on every tick, and charges commission on open and close.

**Used by:** 13 files (see Backlinks)

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/ISimulationBrain.cs.md|ISimulationBrain.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Trader/ITrader.cs.md|ITrader.cs]]
- [[Services/Trader/Orders/PendingOrder.cs.md|PendingOrder.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
