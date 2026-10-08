---
tags: [class, feature/xgrid, kind/data, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data
code_lines: 2
types: 1
methods: 0
fan_in: 4
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridExternalDraft.cs
---
# XGridExternalDraft.cs

A not-yet-submitted order a host previews over the grid without going through XGrid's own click-to-draft flow -- rendered the same way as a click-built draft (see XGrid.BuildPayload), but only while the user isn't actively building one of their own.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 2 · **Used by:** 4

## Types
- `XGridExternalDraft` — record struct, data type (at most one helper method) · 4 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`.

## Uses
- _nothing in Poink.Sim_
