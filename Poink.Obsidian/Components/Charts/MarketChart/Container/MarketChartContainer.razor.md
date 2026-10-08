---
tags: [component, feature/marketchart, kind/component]
role: component
feature: MarketChart
feature_source: folder
kind: component
code_lines: 19
types: 1
methods: 0
fan_in: 10
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/MarketChart/Container/MarketChartContainer.razor
---
# MarketChartContainer.razor

Composes MarketChartToolbar and MarketChart (the canvas) and owns the draw-mode/hint/expanded state that flows between them, so hosts only need to retarget the tag from &lt;MarketChart&gt; to &lt;MarketChartContainer&gt; -- the public parameter surface matches MarketChart's former one.

**Feature:** MarketChart · **Kind:** Razor component · **Code lines:** 19 · **Used by:** 10

## Types
- `MarketChartContainer` — component, Razor component · empty

## Uses
- [[Components/Charts/MarketChart/Control/MarketChartToolbar.razor.md|MarketChartToolbar.razor]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.md|MarketChart.razor]]
