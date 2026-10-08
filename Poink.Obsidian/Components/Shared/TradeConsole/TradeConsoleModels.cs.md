---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 35
types: 5
methods: 0
fan_in: 9
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/TradeConsole/TradeConsoleModels.cs
---
# TradeConsoleModels.cs

A validated order ready to submit -- the host page turns this into whatever its own backend needs.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 35 · **Used by:** 9

## Types
- `TradeConsoleOrderRequest` — record struct, data type (at most one helper method) · 6 properties
- `SizingMode` — enum, enum · 2 values
- `VolumeSizingMode` — enum, enum · 2 values
- `TradeConsoleOptions` — record, data type (at most one helper method) · 10 properties
- `TradeConsoleDraft` — record struct, data type (at most one helper method) · 5 properties

## Uses
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
