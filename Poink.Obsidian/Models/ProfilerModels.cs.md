---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 12
types: 2
methods: 0
fan_in: 6
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Models/ProfilerModels.cs
---
# ProfilerModels.cs

What to load for one Profiler symbol; PriceBucketWidth sizes each bar's price histogram buckets.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 12 · **Used by:** 6

## Types
- `ProfilerConfig` — record, data type (at most one helper method) · 5 properties
- `ProfilerChunk` — record, data type (at most one helper method) · 6 properties

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/QuartileBar.cs.md|QuartileBar.cs]]
