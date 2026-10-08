---
tags: [helper, feature/emulator, kind/mixed, candidate/helper-sprawl, candidate/move-to-feature]
role: helper
feature: Emulator
feature_source: usage
kind: mixed
code_lines: 42
types: 2
methods: 5
fan_in: 3
fan_out: 3
summary_source: documented
candidates: [helper-sprawl, move-to-feature]
source: Poink.Sim/Helpers/EmulatorMarketScaleHelper.cs
---
# EmulatorMarketScaleHelper.cs

Fits one price scale for the chart and the ladder to share: the price history (with the last price and the spread) fills the middle half, and the quarters above and below are left for the order book. Book levels beyond those margins fall off the scale rather than squeezing the history.

**Feature:** Emulator (by usage) · **Kind:** mixed types · **Code lines:** 42 · **Used by:** 3

## Types
- `EmulatorMarketScale` — record, class with behaviour · 4 methods, 6 properties
- `EmulatorMarketScaleHelper` — class, single-method class · 1 method, 4 fields

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.
- **move-to-feature** — Used only by the Emulator feature; move it into that feature's folder.

## Uses
- [[Helpers/EmulatorChartHelper.cs.md|EmulatorChartHelper.cs]]
- [[Helpers/EmulatorTickHelper.cs.md|EmulatorTickHelper.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
