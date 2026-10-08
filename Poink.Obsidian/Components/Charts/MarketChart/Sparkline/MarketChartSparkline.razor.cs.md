---
tags: [code-behind, feature/marketchart, kind/component-code]
role: code-behind
feature: MarketChart
feature_source: folder
kind: component-code
code_lines: 65
types: 1
methods: 2
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/MarketChart/Sparkline/MarketChartSparkline.razor.cs
---
# MarketChartSparkline.razor.cs

Standalone, read-only timeline of a bar list's close prices -- takes its own Bars directly (e.g. ProfilerPage's currently loaded/selected data) rather than syncing to a MarketChart instance, so it has no dependency on one being present, mounted, or interactive.

**Feature:** MarketChart · **Kind:** component code-behind · **Code lines:** 65 · **Used by:** 0

## Types
- `MarketChartSparkline` — class, Razor component · 2 methods, 5 properties, 8 fields

## Uses
- [[Components/Charts/MarketChart/Sparkline/MarketChartSparkline.razor.md|MarketChartSparkline.razor]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[wwwroot/js/marketChartSparkline.js.md|marketChartSparkline.js]]
