---
tags: [model, feature/trade, kind/data-bundle]
role: model
feature: Trade
feature_source: folder
kind: data-bundle
code_lines: 8
types: 2
methods: 0
fan_in: 4
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Models/SymbolCoverageModels.cs
---
# SymbolCoverageModels.cs

A symbol's stored tick history (its live tick table), as recorded in the tick_table_stats registry.

**Feature:** Trade · **Kind:** bundle of data types · **Code lines:** 8 · **Used by:** 4

## Types
- `SymbolCoverageStatus` — enum, enum · 3 values
- `SymbolTickCoverage` — record, data type (at most one helper method) · 6 properties

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
