---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 46
types: 2
methods: 5
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/Indicators/Indicators.razor.cs
---
# Indicators.razor.cs

Single control surface for the Lookback-Window-derived overlays (POC/VAH/VAL/VWAP/AVG/MED/REG, day and interval lines) plus the Lookback Window itself, so every chart a host passes Settings to shows the same levels.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 46 · **Used by:** 0

## Types
- `Indicators` — class, Razor component · 5 methods, 10 properties
- `LevelToggle` — record, nested, data type (at most one helper method) · 5 properties

## Uses
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
