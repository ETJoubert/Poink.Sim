---
tags: [settings, feature/xgrid, kind/data, candidate/merge-data]
role: settings
feature: XGrid
feature_source: folder
kind: data
code_lines: 23
types: 1
methods: 0
fan_in: 4
fan_out: 2
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridDisplaySettings.cs
---
# XGridDisplaySettings.cs

The row/column overlay toggles XGridController owns and passes into XGrid for both editing and rendering -- immutable so a change always flows back through XGrid's DisplayChanged callback rather than being mutated in place.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 23 · **Used by:** 4

## Types
- `XGridDisplaySettings` — record, data type (at most one helper method) · 21 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`.

## Uses
- [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]]
- [[Components/Charts/XGrid/Helper/XGridSweepMode.cs.md|XGridSweepMode.cs]]
