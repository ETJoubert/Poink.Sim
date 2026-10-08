---
tags: [helper, feature/shared, kind/static-helper, candidate/helper-sprawl]
role: helper
feature: Shared
feature_source: shared
kind: static-helper
code_lines: 28
types: 1
methods: 2
fan_in: 2
fan_out: 2
summary_source: documented
candidates: [helper-sprawl]
source: Poink.Sim/Helpers/TickRateHelper.cs
---
# TickRateHelper.cs

Measures recent tick activity from a raw tick list, independent of any bar size.

**Feature:** Shared · **Kind:** static helper · **Code lines:** 28 · **Used by:** 2

## Types
- `TickRateHelper` — class, static helper · 2 methods, 1 field

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.

## Uses
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
