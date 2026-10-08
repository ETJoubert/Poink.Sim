---
tags: [helper, feature/simulation, kind/static-helper, candidate/helper-sprawl, candidate/move-to-feature]
role: helper
feature: Simulation
feature_source: usage
kind: static-helper
code_lines: 21
types: 1
methods: 2
fan_in: 1
fan_out: 4
summary_source: documented
candidates: [helper-sprawl, move-to-feature]
source: Poink.Sim/Helpers/IndicatorRowsHelper.cs
---
# IndicatorRowsHelper.cs

Builds the indicator panel's rows from a run of completed bars, oldest first.

**Feature:** Simulation (by usage) · **Kind:** static helper · **Code lines:** 21 · **Used by:** 1

## Types
- `IndicatorRowsHelper` — class, static helper · 2 methods, 1 field

## Candidates
- **helper-sprawl** — One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`.
- **move-to-feature** — Used only by the Simulation feature; move it into that feature's folder.

## Uses
- [[Components/Shared/IndicatorPanel/IndicatorModels.cs.md|IndicatorModels.cs]]
- [[Components/Shared/IndicatorPanel/IndicatorPanel.razor.md|IndicatorPanel.razor]]
- [[Helpers/MarketRegimeHelper.cs.md|MarketRegimeHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
