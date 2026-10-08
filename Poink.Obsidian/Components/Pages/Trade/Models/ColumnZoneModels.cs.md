---
tags: [model, feature/trade, kind/mixed]
role: model
feature: Trade
feature_source: folder
kind: mixed
code_lines: 33
types: 6
methods: 2
fan_in: 5
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Models/ColumnZoneModels.cs
---
# ColumnZoneModels.cs

One XGrid row along a zone slider: its price band, its summed value, and that value relative to the largest row's magnitude (0 to 1, or -1 to 1 when the value is signed).

**Feature:** Trade · **Kind:** mixed types · **Code lines:** 33 · **Used by:** 5

## Types
- `ColumnZoneCell` — record, data type (at most one helper method) · 4 properties
- `ColumnZoneRange` — record, data type (at most one helper method) · 4 properties
- `ColumnZoneReading` — record, data type (at most one helper method) · 7 properties
- `ColumnZoneSettings` — record, data type (at most one helper method) · 6 properties
- `ColumnZoneGate` — record, class with behaviour · 2 methods, 7 properties
- `ColumnZone` — enum, enum · 5 values

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
