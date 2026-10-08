---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 74
types: 2
methods: 7
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/NeatEvolutionChart/NeatEvolutionChart.razor.cs
---
# NeatEvolutionChart.razor.cs

Plots one point per NEAT generation from already-aggregated stats records, with no rolling window (unlike TrendLineChart's per-episode smoothing).

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 74 · **Used by:** 0

## Types
- `NeatEvolutionChart` — class, Razor component · 7 methods, 14 properties, 7 fields
- `ChartPoint` — record, nested, data type (at most one helper method) · 5 properties

## Uses
- [[Components/Pages/Training/Components/NeatEvolutionChart/NeatEvolutionChart.razor.md|NeatEvolutionChart.razor]]
- [[Services/Brain/Neural/Neat/NeatModels.cs.md|NeatModels.cs]]
