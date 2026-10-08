---
tags: [class, feature/xgrid, kind/data, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data
code_lines: 9
types: 1
methods: 0
fan_in: 4
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridPending.cs
---
# XGridPending.cs

A resting order a host pins over the grid at its trigger price -- drawn as a dotted level with a right-edge direction marker, distinct from live positions. Id makes it addressable so a click can open it for modification or cancellation; Volume/StopLoss/TakeProfit seed the modify panel.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 9 · **Used by:** 4

## Types
- `XGridPending` — record struct, data type (at most one helper method) · 6 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`.

## Uses
- _nothing in Poink.Sim_
