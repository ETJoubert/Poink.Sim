---
tags: [class, feature/xgrid, kind/mixed]
role: class
feature: XGrid
feature_source: folder
kind: mixed
code_lines: 18
types: 2
methods: 3
fan_in: 3
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Display/XGridLine.cs
---
# XGridLine.cs

A line overlaid on the XGrid, anchored to absolute price/time rather than grid cell indices so it tracks its real position as the grid recomputes. Segment joins two price/time points; Vertical pins to a time and spans every price; Horizontal pins to a price and spans every time.

**Feature:** XGrid · **Kind:** mixed types · **Code lines:** 18 · **Used by:** 3

## Types
- `XGridLineKind` — enum, enum · 3 values
- `XGridLine` — record, class with behaviour · 3 methods, 6 properties

## Uses
- _nothing in Poink.Sim_
