---
tags: [model, feature/shared, kind/data]
role: model
feature: Shared
feature_source: shared
kind: data
code_lines: 22
types: 1
methods: 0
fan_in: 14
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/Indicators/IndicatorsModels.cs
---
# IndicatorsModels.cs

Which Lookback-Window-derived levels and time lines are overlaid on every chart the Indicators component drives. Immutable so a change always flows back through the component's SettingsChanged callback.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 22 · **Used by:** 14

## Types
- `IndicatorSettings` — record, data type (at most one helper method) · 20 properties

## Uses
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
