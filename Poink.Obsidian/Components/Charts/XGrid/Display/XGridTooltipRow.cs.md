---
tags: [class, feature/xgrid, kind/data, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data
code_lines: 7
types: 1
methods: 0
fan_in: 1
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Display/XGridTooltipRow.cs
---
# XGridTooltipRow.cs

One label/value line in a hover tooltip (cell or line) -- returned as a list so xGrid.js can render each as its own row instead of one long joined string. Property names are pinned explicitly since this crosses the JS interop boundary. Tone optionally colors the row (e.g. "gain"/"loss") -- cell rows never set it.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 7 · **Used by:** 1

## Types
- `XGridTooltipRow` — record struct, data type (at most one helper method) · 3 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`.

## Uses
- _nothing in Poink.Sim_
