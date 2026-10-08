---
tags: [helper, feature/emulator, kind/mixed, candidate/helper-sprawl, candidate/move-to-feature]
role: helper
feature: Emulator
feature_source: usage
kind: mixed
code_lines: 19
types: 2
methods: 1
fan_in: 1
fan_out: 1
summary_source: documented
candidates: [helper-sprawl, move-to-feature]
source: Poink.Sim/Helpers/EmulatorGroupRankingHelper.cs
---
# EmulatorGroupRankingHelper.cs

Ranks groups by return on the capital paid in and awards gold, silver and bronze to the top three. Ties go to the name that sorts first so medals do not flicker between equal groups; a lone group wins nothing.

**Feature:** Emulator (by usage) · **Kind:** mixed types · **Code lines:** 19 · **Used by:** 1

## Types
- `EmulatorMedal` — enum, enum · 3 values
- `EmulatorGroupRankingHelper` — class, single-method class · 1 method, 1 field

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.
- **move-to-feature** — Used only by the Emulator feature; move it into that feature's folder.

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
