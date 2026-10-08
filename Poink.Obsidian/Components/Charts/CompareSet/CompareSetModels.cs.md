---
tags: [model, feature/compareset, kind/data-bundle]
role: model
feature: CompareSet
feature_source: folder
kind: data-bundle
code_lines: 31
types: 6
methods: 0
fan_in: 7
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareSet/CompareSetModels.cs
---
# CompareSetModels.cs

What the compare chart measures each symbol's % change from.

**Feature:** CompareSet · **Kind:** bundle of data types · **Code lines:** 31 · **Used by:** 7

## Types
- `CompareSetMode` — enum, enum · 2 values
- `CompareSetAnchor` — enum, enum · 2 values
- `CompareSetSymbol` — record, data type (at most one helper method) · 4 properties
- `CompareSetTxFoldSettings` — record, data type (at most one helper method) · 5 properties
- `CompareSetXGridSettings` — record, data type (at most one helper method) · 8 properties
- `CompareSetPanel` — enum, enum · 3 values

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Components/Charts/CompareXGrid/CompareXGridModels.cs.md|CompareXGridModels.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
