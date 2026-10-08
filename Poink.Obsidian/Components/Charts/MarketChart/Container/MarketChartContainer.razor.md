---
tags: [component]
source: Poink.Sim/Components/Charts/MarketChart/Container/MarketChartContainer.razor
---
# MarketChartContainer.razor

Composes MarketChartToolbar and MarketChart (the canvas) and owns the draw-mode/hint/expanded state that flows between them, so hosts only need to retarget the tag from &lt;MarketChart&gt; to &lt;MarketChartContainer&gt; -- the public parameter surface matches MarketChart's former one.

**Used by:** 10 files (see Backlinks)

## Uses
- [[Components/Charts/MarketChart/Control/MarketChartToolbar.razor.md|MarketChartToolbar.razor]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.md|MarketChart.razor]]
