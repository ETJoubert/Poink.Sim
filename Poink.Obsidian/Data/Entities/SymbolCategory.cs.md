---
tags: [class, feature/shared, kind/data]
role: class
feature: Shared
feature_source: shared
kind: data
code_lines: 16
types: 1
methods: 0
fan_in: 2
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Data/Entities/SymbolCategory.cs
---
# SymbolCategory.cs

One row per cTrader ProtoOASymbolCategory, correlated by SymbolCategoryId rather than a DB-enforced foreign key, matching how SymbolMetadata correlates to Symbol.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 16 · **Used by:** 2

## Types
- `SymbolCategory` — class, data type (at most one helper method) · 8 properties

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
