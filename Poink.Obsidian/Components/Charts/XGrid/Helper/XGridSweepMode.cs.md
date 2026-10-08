---
tags: [enum, feature/xgrid, kind/enum, candidate/merge-data]
role: enum
feature: XGrid
feature_source: folder
kind: enum
code_lines: 6
types: 1
methods: 0
fan_in: 2
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridSweepMode.cs
---
# XGridSweepMode.cs

Which precomputed sweep array (XGridResult.SweepLeftToRight/SweepRightToLeft/SweepSum) the grid shades rows by, or None to disable. Public since XGridDisplaySettings exposes it.

**Feature:** XGrid · **Kind:** enum · **Code lines:** 6 · **Used by:** 2

## Types
- `XGridSweepMode` — enum, enum · 4 values

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`.

## Uses
- _nothing in Poink.Sim_
