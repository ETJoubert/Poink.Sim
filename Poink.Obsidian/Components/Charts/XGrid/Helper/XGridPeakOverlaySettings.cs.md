---
tags: [settings, feature/xgrid, kind/data, candidate/merge-data]
role: settings
feature: XGrid
feature_source: folder
kind: data
code_lines: 8
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridPeakOverlaySettings.cs
---
# XGridPeakOverlaySettings.cs

Swing-low/-high overlay config for one direction, edited by XGrid's Highlights row and used by XGrid for ranking via Settings). Immutable for the same reason as XGridDisplaySettings.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 8 · **Used by:** 3

## Types
- `XGridPeakOverlaySettings` — record, data type (at most one helper method) · 6 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`.

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
