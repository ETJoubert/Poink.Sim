---
tags: [class, feature/txfold, kind/mixed]
role: class
feature: TxFold
feature_source: folder
kind: mixed
code_lines: 70
types: 4
methods: 6
fan_in: 7
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/TxFold/Helper/TxFoldStats.cs
---
# TxFoldStats.cs

Levels is null only when there are no ticks at all -- unlike Poc/Vah/Val inside it, which can independently be null just from too little data for a meaningful value area.

**Feature:** TxFold · **Kind:** mixed types · **Code lines:** 70 · **Used by:** 7

## Types
- `TxFoldVolumeBucket` — record struct, data type (at most one helper method) · 3 properties
- `TxFoldBar` — record struct, data type (at most one helper method) · 5 properties
- `TxFoldSnapshot` — record struct, data type (at most one helper method) · 3 properties
- `TxFoldStats` — class, static helper · 6 methods

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Helpers/BarAggregator.cs.md|BarAggregator.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
