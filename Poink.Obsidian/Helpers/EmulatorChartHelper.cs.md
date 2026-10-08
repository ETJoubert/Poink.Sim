---
tags: [helper, feature/emulator, kind/mixed, candidate/move-to-feature]
role: helper
feature: Emulator
feature_source: usage
kind: mixed
code_lines: 82
types: 2
methods: 11
fan_in: 11
fan_out: 1
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Helpers/EmulatorChartHelper.cs
---
# EmulatorChartHelper.cs

Maps the Emulator's prices and book volumes onto chart and depth-bar geometry.

**Feature:** Emulator (by usage) · **Kind:** mixed types · **Code lines:** 82 · **Used by:** 11

## Types
- `EmulatorPriceRange` — record struct, data type (at most one helper method) · 3 properties
- `EmulatorChartHelper` — class, static helper · 11 methods, 1 field

## Candidates
- **move-to-feature** — Used only by the Emulator feature; move it into that feature's folder.

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
