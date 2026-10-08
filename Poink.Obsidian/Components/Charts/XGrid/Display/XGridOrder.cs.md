---
tags: [class, feature/xgrid, kind/data, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data
code_lines: 8
types: 1
methods: 0
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridOrder.cs
---
# XGridOrder.cs

An order drafted on the XGrid's right-margin panel and handed to a host via OnOrderPlaced. Prices are absolute so the host maps them onto its own order model; StopLoss/TakeProfit are null until the user pins them.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 8 · **Used by:** 3

## Types
- `XGridOrder` — record struct, data type (at most one helper method) · 5 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`.

## Uses
- _nothing in Poink.Sim_
