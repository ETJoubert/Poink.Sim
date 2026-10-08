---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 14
types: 1
methods: 1
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/StatisticsPanel/StatisticsPanel.razor.cs
---
# StatisticsPanel.razor.cs

Renders TradeStatistics.Groups as labeled sections, instead of ViewDatatable's single flat key/value table, with its own scoped styling since scoped CSS doesn't reach a child component.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 14 · **Used by:** 0

## Types
- `StatisticsPanel` — class, Razor component · 1 method, 2 properties

## Uses
- [[Components/Shared/StatisticsPanel/StatisticsPanel.razor.md|StatisticsPanel.razor]]
- [[Models/StatGroup.cs.md|StatGroup.cs]]
