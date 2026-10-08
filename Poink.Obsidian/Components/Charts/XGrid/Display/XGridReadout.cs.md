---
tags: [class, feature/xgrid, kind/data, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data
code_lines: 2
types: 1
methods: 0
fan_in: 2
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridReadout.cs
---
# XGridReadout.cs

One labelled reading shown in XGrid's footer readout rows and its cell tooltip. Description feeds the footer's tooltip; Tone ("up"/"down") colours the footer value only.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 2 · **Used by:** 2

## Types
- `XGridReadout` — record struct, data type (at most one helper method) · 4 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridTooltipRow.cs`.

## Uses
- _nothing in Poink.Sim_
