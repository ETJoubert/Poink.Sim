---
tags: [helper, feature/shared, kind/static-helper, candidate/helper-sprawl]
role: helper
feature: Shared
feature_source: shared
kind: static-helper
code_lines: 20
types: 1
methods: 2
fan_in: 8
fan_out: 0
summary_source: documented
candidates: [helper-sprawl]
source: Poink.Sim/Helpers/SymbolColorHelper.cs
---
# SymbolColorHelper.cs

Assigns each selected symbol a theme colour by its selection order, so a page's symbols panel works as the legend for every chart set and the compare chart -- shared by the Profiler and Simulation pages so both colour alike.

**Feature:** Shared · **Kind:** static helper · **Code lines:** 20 · **Used by:** 8

## Types
- `SymbolColorHelper` — class, static helper · 2 methods, 2 fields

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.

## Uses
- _nothing in Poink.Sim_
