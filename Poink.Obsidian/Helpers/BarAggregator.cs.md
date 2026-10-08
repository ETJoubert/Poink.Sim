---
tags: [class, feature/txfold, kind/mixed, candidate/move-to-feature]
role: class
feature: TxFold
feature_source: usage
kind: mixed
code_lines: 54
types: 2
methods: 3
fan_in: 1
fan_out: 3
summary_source: generated
candidates: [move-to-feature]
source: Poink.Sim/Helpers/BarAggregator.cs
---
# BarAggregator.cs

Defines bar aggregator.

**Feature:** TxFold (by usage) · **Kind:** mixed types · **Code lines:** 54 · **Used by:** 1

## Types
- `AggregatedBar` — record struct, data type (at most one helper method) · 3 properties
- `BarAggregator` — class, static helper · 3 methods

## Candidates
- **move-to-feature** — Used only by the TxFold feature; move it into that feature's folder.

## Uses
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
