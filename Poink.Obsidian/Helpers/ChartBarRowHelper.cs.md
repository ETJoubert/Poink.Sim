---
tags: [helper, feature/profiler, kind/static-helper, candidate/move-to-feature]
role: helper
feature: Profiler
feature_source: usage
kind: static-helper
code_lines: 23
types: 1
methods: 3
fan_in: 2
fan_out: 4
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Helpers/ChartBarRowHelper.cs
---
# ChartBarRowHelper.cs

Projects aggregated bars into the CTraderBarRow/line-point shapes the market charts render.

**Feature:** Profiler (by usage) · **Kind:** static helper · **Code lines:** 23 · **Used by:** 2

## Types
- `ChartBarRowHelper` — class, static helper · 3 methods

## Candidates
- **move-to-feature** — Used only by the Profiler feature; move it into that feature's folder.

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/QuartileBar.cs.md|QuartileBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
