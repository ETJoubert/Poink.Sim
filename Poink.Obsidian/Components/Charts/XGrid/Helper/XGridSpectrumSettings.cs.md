---
tags: [settings, feature/xgrid, kind/data, candidate/merge-data]
role: settings
feature: XGrid
feature_source: folder
kind: data
code_lines: 15
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridSpectrumSettings.cs
---
# XGridSpectrumSettings.cs

The rank/highlight overlay toggles for XGrid's Total/Up/Down/Delta+/Delta- spectrums, edited by XGrid's Highlights row and drawn as its canvas highlight borders. Immutable for the same reason as XGridDisplaySettings.

**Feature:** XGrid · **Kind:** data type (at most one helper method) · **Code lines:** 15 · **Used by:** 3

## Types
- `XGridSpectrumSettings` — record, data type (at most one helper method) · 13 properties

## Candidates
- **merge-data** — Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSweepMode.cs`.

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
