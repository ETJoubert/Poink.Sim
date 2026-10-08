---
tags: [class, feature/xgrid, kind/data, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data
code_lines: 9
types: 1
methods: 0
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridOrderModification.cs
---
# XGridOrderModification.cs

A change to a resting order, raised when the user applies edits on the pending panel. The engine cannot modify a resting order in place, so the host cancels PendingId and re-places it with these values.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 9 · **Used by:** 3

## Types
- `XGridOrderModification` — record struct, data type (at most one helper method) · 6 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`.

## Uses
- _nothing in Poink.Sim_
