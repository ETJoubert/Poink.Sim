---
tags: [model, feature/trade, kind/data]
role: model
feature: Trade
feature_source: folder
kind: data
code_lines: 10
types: 1
methods: 0
fan_in: 2
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Models/SymbolFilterModels.cs
---
# SymbolFilterModels.cs

The Trade symbol search's active filters; empty sets and false flags each mean "don't narrow".

**Feature:** Trade · **Kind:** data type (at most one helper method) · **Code lines:** 10 · **Used by:** 2

## Types
- `SymbolFilterCriteria` — record, data type (at most one helper method) · 5 properties

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
