---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 38
types: 3
methods: 0
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/PositionTable/PositionTableModels.cs
---
# PositionTableModels.cs

Display-ready projection of one open position for PositionTable -- formatting and derived figures are computed once per row, not once per column per render. TPosition is the host's own backend identity (e.g.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 38 · **Used by:** 3

## Types
- `PositionRowViewModel` — record, data type (at most one helper method) · 26 properties
- `PositionColumn` — record, data type (at most one helper method) · 4 properties
- `PositionColumnGroup` — record, data type (at most one helper method) · 2 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationRowCache.cs.md|SimulationRowCache.cs]]
- [[Components/Shared/PositionTable/PositionTable.razor.md|PositionTable.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
