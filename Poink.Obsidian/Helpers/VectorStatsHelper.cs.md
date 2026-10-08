---
tags: [helper, feature/shared, kind/mixed, candidate/helper-sprawl]
role: helper
feature: Shared
feature_source: shared
kind: mixed
code_lines: 21
types: 2
methods: 1
fan_in: 1
fan_out: 0
summary_source: generated
candidates: [helper-sprawl]
source: Poink.Sim/Helpers/VectorStatsHelper.cs
---
# VectorStatsHelper.cs

Helper methods for vector stats.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 21 · **Used by:** 1

## Types
- `VectorPoint` — record struct, data type (at most one helper method) · 2 properties
- `VectorStatsHelper` — class, single-method class · 1 method

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`.

## Uses
- _nothing in Poink.Sim_
