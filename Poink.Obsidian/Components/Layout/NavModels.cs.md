---
tags: [model, feature/layout, kind/data-bundle]
role: model
feature: Layout
feature_source: folder
kind: data-bundle
code_lines: 5
types: 4
methods: 0
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Layout/NavModels.cs
---
# NavModels.cs

Common shape for a top-level entry in the main nav: a direct link, a dropdown group, or an always-visible cluster of links.

**Feature:** Layout · **Kind:** bundle of data types · **Code lines:** 5 · **Used by:** 2

## Types
- `NavEntry` — record, data type (at most one helper method) · 2 properties
- `NavLinkItem` — record, data type (at most one helper method) · 3 properties
- `NavGroupItem` — record, data type (at most one helper method) · 3 properties
- `NavClusterItem` — record, data type (at most one helper method) · 4 properties

## Uses
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
