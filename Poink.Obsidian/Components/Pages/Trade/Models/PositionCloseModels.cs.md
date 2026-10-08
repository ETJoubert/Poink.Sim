---
tags: [model, feature/trade, kind/data-bundle]
role: model
feature: Trade
feature_source: folder
kind: data-bundle
code_lines: 16
types: 6
methods: 0
fan_in: 6
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Models/PositionCloseModels.cs
---
# PositionCloseModels.cs

One side of one symbol whose open positions close as each gets back to its entry price or better.

**Feature:** Trade · **Kind:** bundle of data types · **Code lines:** 16 · **Used by:** 6

## Types
- `BreakevenSide` — record struct, data type (at most one helper method) · 3 properties
- `BreakevenArmedEntry` — record, data type (at most one helper method) · 3 properties
- `CloseAutomationSettings` — record, data type (at most one helper method) · 6 properties
- `CloseAutomationScope` — record, data type (at most one helper method) · 2 properties
- `CloseCountdown` — record, data type (at most one helper method) · 5 properties
- `PositionCloseReason` — enum, enum · 3 values

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
