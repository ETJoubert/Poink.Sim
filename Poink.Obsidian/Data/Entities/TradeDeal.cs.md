---
tags: [class]
source: Poink.Sim/Data/Entities/TradeDeal.cs
---
# TradeDeal.cs

A closed-position deal persisted from cTrader's account history, correlated to the account/position/symbol by their raw cTrader ids rather than a DB-enforced foreign key, matching how the rest of the app correlates cTrader data.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
