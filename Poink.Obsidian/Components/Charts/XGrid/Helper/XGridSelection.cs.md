---
tags: [class, feature/xgrid, kind/data-bundle, candidate/merge-data]
role: class
feature: XGrid
feature_source: folder
kind: data-bundle
code_lines: 11
types: 2
methods: 0
fan_in: 4
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridSelection.cs
---
# XGridSelection.cs

A selected cell, row or column by grid index; Row is ignored for a column and Col for a row.

**Feature:** XGrid · **Kind:** bundle of data types · **Code lines:** 11 · **Used by:** 4

## Types
- `XGridSelectionKind` — enum, enum · 3 values
- `XGridSelection` — record struct, data type (at most one helper method) · 4 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`.

## Uses
- _nothing in Poink.Sim_
