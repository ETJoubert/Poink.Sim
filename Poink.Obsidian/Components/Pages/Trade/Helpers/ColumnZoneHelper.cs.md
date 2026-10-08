---
tags: [helper, feature/trade, kind/static-helper]
role: helper
feature: Trade
feature_source: folder
kind: static-helper
code_lines: 96
types: 1
methods: 18
fan_in: 4
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Helpers/ColumnZoneHelper.cs
---
# ColumnZoneHelper.cs

Reads XGrid's recent columns into price spans and places the current price in each one's low, middle or high band.

**Feature:** Trade · **Kind:** static helper · **Code lines:** 96 · **Used by:** 4

## Types
- `ColumnZoneHelper` — class, static helper · 18 methods, 7 fields

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Trade/Models/ColumnZoneModels.cs.md|ColumnZoneModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
