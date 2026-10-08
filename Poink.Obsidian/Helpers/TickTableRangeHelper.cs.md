---
tags: [helper, feature/shared, kind/static-helper, candidate/helper-sprawl]
role: helper
feature: Shared
feature_source: shared
kind: static-helper
code_lines: 8
types: 1
methods: 2
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [helper-sprawl]
source: Poink.Sim/Helpers/TickTableRangeHelper.cs
---
# TickTableRangeHelper.cs

Resolves the From/To a ticks_&lt;symbol&gt; table opens on when it is picked, so every symbol picker lands on the same slice of history.

**Feature:** Shared · **Kind:** static helper · **Code lines:** 8 · **Used by:** 3

## Types
- `TickTableRangeHelper` — class, static helper · 2 methods, 1 field

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `VectorStatsHelper.cs`.

## Uses
- _nothing in Poink.Sim_
