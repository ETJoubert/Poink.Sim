---
tags: [class, feature/shared, kind/data]
role: class
feature: Shared
feature_source: shared
kind: data
code_lines: 22
types: 1
methods: 0
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Data/Entities/TradeDeal.cs
---
# TradeDeal.cs

A closed-position deal persisted from cTrader's account history, correlated to the account/position/symbol by their raw cTrader ids rather than a DB-enforced foreign key, matching how the rest of the app correlates cTrader data.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 22 · **Used by:** 2

## Types
- `TradeDeal` — class, data type (at most one helper method) · 14 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
