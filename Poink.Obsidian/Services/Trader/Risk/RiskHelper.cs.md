---
tags: [helper]
source: Poink.Sim/Services/Trader/Risk/RiskHelper.cs
---
# RiskHelper.cs

Stateless risk calculations shared by simulation and training. Position sizing mirrors Trader: committed = fraction * cash, notional = committed * leverage, size = notional / price. Commission is excluded.

**Used by:** 15 files (see Backlinks)

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
