---
tags: [enum, feature/marketchart, kind/enum]
role: enum
feature: MarketChart
feature_source: folder
kind: enum
code_lines: 12
types: 1
methods: 0
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/MarketChart/Helper/DrawMode.cs
---
# DrawMode.cs

TRange/PRange/Rectangle are built from repeated H/V-line placement, not their own shape; VolumeRange is unrelated to line placement entirely.

**Feature:** MarketChart · **Kind:** enum · **Code lines:** 12 · **Used by:** 4

## Types
- `DrawMode` — enum, enum · 10 values

## Uses
- [[Repository/Interfaces/ITRangeChartRepository.cs.md|ITRangeChartRepository.cs]]
- [[Repository/Interfaces/ITRangeReferenceChartRepository.cs.md|ITRangeReferenceChartRepository.cs]]
