---
tags: [settings, feature/shared, kind/single-method, candidate/helper-sprawl, candidate/single-method]
role: settings
feature: Shared
feature_source: shared
kind: single-method
code_lines: 31
types: 1
methods: 1
fan_in: 2
fan_out: 0
summary_source: generated
candidates: [helper-sprawl, single-method]
source: Poink.Sim/Helpers/BarSizeOptions.cs
---
# BarSizeOptions.cs

Configuration settings for bar size.

**Feature:** Shared · **Kind:** single-method class · **Code lines:** 31 · **Used by:** 2

## Types
- `BarSizeOptions` — class, single-method class · 1 method, 1 field

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.
- **single-method** — Fold `Label` into a sibling helper (`ChartBarRowHelper.cs`, `DifferentialHelper.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorTickHelper.cs`, `IndicatorRowsHelper.cs`, `MarketRegimeHelper.cs`, `SplitTickReadHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`).

## Uses
- _nothing in Poink.Sim_
