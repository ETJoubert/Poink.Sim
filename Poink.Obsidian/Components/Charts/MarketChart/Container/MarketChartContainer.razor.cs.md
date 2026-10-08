---
tags: [code-behind, feature/marketchart, kind/component-code]
role: code-behind
feature: MarketChart
feature_source: folder
kind: component-code
code_lines: 83
types: 1
methods: 5
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/MarketChart/Container/MarketChartContainer.razor.cs
---
# MarketChartContainer.razor.cs

Composes MarketChartToolbar and MarketChart (the canvas) and owns the draw-mode/hint/expanded state that flows between them, so hosts only need to retarget the tag from &lt;MarketChart&gt; to &lt;MarketChartContainer&gt; -- the public parameter surface matches MarketChart's former one.

**Feature:** MarketChart · **Kind:** component code-behind · **Code lines:** 83 · **Used by:** 0

## Types
- `MarketChartContainer` — class, Razor component · 5 methods, 30 properties, 3 fields

## Uses
- [[Components/Charts/MarketChart/Container/MarketChartContainer.razor.md|MarketChartContainer.razor]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/MarketChart/Helper/DrawMode.cs.md|DrawMode.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
