---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 43
types: 5
methods: 0
fan_in: 77
fan_out: 4
summary_source: generated
candidates: []
source: Poink.Sim/Models/SimulationDecision.cs
---
# SimulationDecision.cs

Models, records and DTOs for simulation decision.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 43 · **Used by:** 77

## Types
- `SimulationAction` — enum, enum · 4 values
- `OrderType` — enum, enum · 3 values
- `SimulationDecision` — record struct, data type (at most one helper method) · 7 properties
- `SimulationObservation` — record struct, data type (at most one helper method) · 10 properties
- `MarketWindow` — record struct, data type (at most one helper method) · 7 properties

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
