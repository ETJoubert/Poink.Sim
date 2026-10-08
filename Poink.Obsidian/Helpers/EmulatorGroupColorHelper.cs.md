---
tags: [helper, feature/emulator, kind/single-method, candidate/helper-sprawl, candidate/single-method, candidate/move-to-feature]
role: helper
feature: Emulator
feature_source: usage
kind: single-method
code_lines: 4
types: 1
methods: 1
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [helper-sprawl, single-method, move-to-feature]
source: Poink.Sim/Helpers/EmulatorGroupColorHelper.cs
---
# EmulatorGroupColorHelper.cs

Colours bot groups from the page's validated categorical palette (--emu-group-1 to --emu-group-8), in fixed order by the group's position. Past the palette the colours repeat, so a group's name is always shown beside its colour and identity never rests on colour alone.

**Feature:** Emulator (by usage) · **Kind:** single-method class · **Code lines:** 4 · **Used by:** 3

## Types
- `EmulatorGroupColorHelper` — class, single-method class · 1 method, 1 field

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.
- **single-method** — Fold `ColorVar` into a sibling helper (`BarSizeOptions.cs`, `ChartBarRowHelper.cs`, `DifferentialHelper.cs`, `EmulatorTickHelper.cs`, `IndicatorRowsHelper.cs`, `MarketRegimeHelper.cs`, `SplitTickReadHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`).
- **move-to-feature** — Used only by the Emulator feature; move it into that feature's folder.

## Uses
- _nothing in Poink.Sim_
