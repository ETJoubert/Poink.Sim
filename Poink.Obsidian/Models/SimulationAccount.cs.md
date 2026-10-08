---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 60
types: 7
methods: 1
fan_in: 106
fan_out: 1
summary_source: generated
candidates: []
source: Poink.Sim/Models/SimulationAccount.cs
---
# SimulationAccount.cs

Models, records and DTOs for simulation account.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 60 · **Used by:** 106

## Types
- `PositionDirection` — enum, enum · 2 values
- `TradeCloseReason` — enum, enum · 4 values
- `Position` — record struct, data type (at most one helper method) · 1 method, 10 properties
- `CapitalState` — record struct, data type (at most one helper method) · 7 properties
- `Trade` — record, data type (at most one helper method) · 11 properties
- `TradeExecution` — record struct, data type (at most one helper method) · 8 properties
- `MaintenanceResult` — record struct, data type (at most one helper method) · 2 properties

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
