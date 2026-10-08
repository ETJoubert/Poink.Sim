---
tags: [class, feature/shared, kind/data]
role: class
feature: Shared
feature_source: shared
kind: data
code_lines: 49
types: 1
methods: 0
fan_in: 4
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Data/Entities/SymbolMetadata.cs
---
# SymbolMetadata.cs

Full per-symbol trading detail from cTrader's ProtoOASymbol (digits, volume/commission/swap rules), correlated to Symbol by SymbolId rather than a DB-enforced foreign key, matching how the rest of the app correlates cTrader data by symbol id.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 49 · **Used by:** 4

## Types
- `SymbolMetadata` — class, data type (at most one helper method) · 41 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Data/Entities/SymbolHoliday.cs.md|SymbolHoliday.cs]]
- [[Data/Entities/SymbolScheduleInterval.cs.md|SymbolScheduleInterval.cs]]
