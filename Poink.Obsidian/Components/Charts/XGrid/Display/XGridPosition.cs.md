---
tags: [class, feature/xgrid, kind/data-bundle, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data-bundle
code_lines: 15
types: 2
methods: 0
fan_in: 6
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridPosition.cs
---
# XGridPosition.cs

A trade overlaid on the XGrid, anchored to absolute price/time like XGridLine so it tracks its real position as the grid recomputes. A null CloseTime/ClosePrice marks a still-open trade: the renderer runs its line to the current price and shows a right-edge direction marker coloured by live profit/loss.

**Feature:** XGrid · **Kind:** bundle of data types · **Code lines:** 15 · **Used by:** 6

## Types
- `XGridPositionDirection` — enum, enum · 2 values
- `XGridPosition` — record, data type (at most one helper method) · 8 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`.

## Uses
- _nothing in Poink.Sim_
