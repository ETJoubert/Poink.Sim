---
tags: [code-behind]
source: Poink.Sim/Components/Charts/MarketChart/Container/MarketChartContainer.razor.cs
---
# MarketChartContainer.razor.cs

Composes MarketChartToolbar and MarketChart (the canvas) and owns the draw-mode/hint/expanded state that flows between them, so hosts only need to retarget the tag from &lt;MarketChart&gt; to &lt;MarketChartContainer&gt; -- the public parameter surface matches MarketChart's former one.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Charts/MarketChart/Container/MarketChartContainer.razor.md|MarketChartContainer.razor]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/MarketChart/Helper/DrawMode.cs.md|DrawMode.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
