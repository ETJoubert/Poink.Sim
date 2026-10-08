---
tags: [code-behind, feature/marketchart, kind/component-code]
role: code-behind
feature: MarketChart
feature_source: folder
kind: component-code
code_lines: 376
types: 9
methods: 15
fan_in: 16
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/MarketChart/Display/MarketChart.razor.cs
---
# MarketChart.razor.cs

Candlestick + volume chart with click-to-draw annotations, shared by ProfilerPage and SimulationPage's live overlay.

**Feature:** MarketChart · **Kind:** component code-behind · **Code lines:** 376 · **Used by:** 16

## Types
- `ChartHorizontalLine` — record struct, data type (at most one helper method) · 3 properties
- `ChartVerticalLine` — record struct, data type (at most one helper method) · 3 properties
- `ChartDiagonalLine` — record struct, data type (at most one helper method) · 6 properties
- `ChartTimeRange` — record struct, data type (at most one helper method) · 2 properties
- `ChartRangeFill` — record struct, data type (at most one helper method) · 4 properties
- `ChartLinePoint` — record struct, data type (at most one helper method) · 2 properties
- `ChartMarker` — record struct, data type (at most one helper method) · 4 properties
- `MarketChart` — class, Razor component · 15 methods, 32 properties, 23 fields
- `BarsChange` — enum, nested, enum · 4 values

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.md|MarketChart.razor]]
- [[Components/Charts/MarketChart/Helper/DrawMode.cs.md|DrawMode.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[wwwroot/js/marketChart.js.md|marketChart.js]]
