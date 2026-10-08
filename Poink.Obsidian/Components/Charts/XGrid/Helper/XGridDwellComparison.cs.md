---
tags: [enum, feature/xgrid, kind/enum, candidate/merge-data]
role: enum
feature: XGrid
feature_source: folder
kind: enum
code_lines: 4
types: 1
methods: 0
fan_in: 4
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridDwellComparison.cs
---
# XGridDwellComparison.cs

Which side of XGridDisplaySettings.DwellSeconds a cell's dwell time must fall on for the Dwell highlight. Public since XGridDisplaySettings exposes it.

**Feature:** XGrid · **Kind:** enum · **Code lines:** 4 · **Used by:** 4

## Types
- `XGridDwellComparison` — enum, enum · 2 values

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`.

## Uses
- _nothing in Poink.Sim_
