---
tags: [class, feature/layout, kind/data, candidate/move-to-feature]
role: class
feature: Layout
feature_source: usage
kind: data
code_lines: 6
types: 1
methods: 1
fan_in: 1
fan_out: 0
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Components/Shared/PageNav/PageNavState.cs
---
# PageNavState.cs

Single source of truth for the page-header nav layout cycle every page (and MainLayout) exposes via its "$ BRAND" click target: horizontal-expanded, horizontal-collapsed, sidebar-expanded, sidebar-collapsed, back to horizontal-expanded.

**Feature:** Layout (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 6 · **Used by:** 1

## Types
- `PageNavState` — class, data type (at most one helper method) · 1 method, 3 properties

## Candidates
- **move-to-feature** — Used only by the Layout feature; move it into that feature's folder.

## Uses
- _nothing in Poink.Sim_
