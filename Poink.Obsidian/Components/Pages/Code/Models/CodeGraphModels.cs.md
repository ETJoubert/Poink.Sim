---
tags: [model, feature/code, kind/mixed]
role: model
feature: Code
feature_source: folder
kind: mixed
code_lines: 60
types: 13
methods: 1
fan_in: 6
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Code/Models/CodeGraphModels.cs
---
# CodeGraphModels.cs

Left-to-right layout region a card is grouped into on the code diagram.

**Feature:** Code · **Kind:** mixed types · **Code lines:** 60 · **Used by:** 6

## Types
- `CodeConcept` — enum, enum · 9 values
- `CodeRegion` — enum, enum · 6 values
- `CodeConceptExtensions` — class, single-method class · 1 method
- `CodeEdgeKind` — enum, enum · 4 values
- `CodePort` — record, data type (at most one helper method) · 1 property
- `CodePortView` — record, data type (at most one helper method) · 5 properties
- `CodeSetting` — record, data type (at most one helper method) · 3 properties
- `CodeSettingView` — record, data type (at most one helper method) · 4 properties
- `CodeTypeRef` — record, data type (at most one helper method) · 2 properties
- `CodeBadge` — record, data type (at most one helper method) · 2 properties
- `CodeNode` — record, data type (at most one helper method) · 12 properties
- `CodeEdge` — record, data type (at most one helper method) · 5 properties
- `CodeGraph` — record, data type (at most one helper method) · 2 properties

## Uses
- [[Services/Trader/Trader.cs.md|Trader.cs]]
