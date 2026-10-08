---
tags: [interface, feature/comparetxfold, kind/interface]
role: interface
feature: CompareTxFold
feature_source: folder
kind: interface
code_lines: 6
types: 1
methods: 1
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareTxFold/ICompareTxFoldTickSource.cs
---
# ICompareTxFoldTickSource.cs

Lets a host supply CompareTxFold's ticks instead of the tick tables, e.g. stored history joined to live ticks not yet written, so the snapshot can follow every tick. Called off the render thread, once per symbol.

**Feature:** CompareTxFold · **Kind:** interface · **Code lines:** 6 · **Used by:** 4

## Types
- `ICompareTxFoldTickSource` — interface, interface · 1 method

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
