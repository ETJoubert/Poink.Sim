---
tags: [class, feature/shared, kind/single-method, candidate/single-method]
role: class
feature: Shared
feature_source: shared
kind: single-method
code_lines: 41
types: 1
methods: 1
fan_in: 2
fan_out: 3
summary_source: documented
candidates: [single-method]
source: Poink.Sim/Helpers/TxFoldBarsCache.cs
---
# TxFoldBarsCache.cs

Loads and caches the TxFold mean/stddev bars the rule engine's TxFold rows read, refetching only when the anchor, range or selected timeframes change -- shared by the rule panel and the robot so neither relies on a chart having already loaded the data.

**Feature:** Shared · **Kind:** single-method class · **Code lines:** 41 · **Used by:** 2

## Types
- `TxFoldBarsCache` — class, single-method class · 1 method, 1 property, 2 fields

## Candidates
- **single-method** — `RefreshIfNeededAsync` is used by 2 files; check it earns its own type.

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
