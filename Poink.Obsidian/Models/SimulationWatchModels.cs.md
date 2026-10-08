---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 15
types: 2
methods: 0
fan_in: 5
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Models/SimulationWatchModels.cs
---
# SimulationWatchModels.cs

A symbol replayed alongside the traded one, watch-only; its pip size sizes its price buckets.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 15 · **Used by:** 5

## Types
- `SimulationWatchSymbol` — record, data type (at most one helper method) · 2 properties
- `WatchedSymbolSnapshot` — record, data type (at most one helper method) · 8 properties

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
