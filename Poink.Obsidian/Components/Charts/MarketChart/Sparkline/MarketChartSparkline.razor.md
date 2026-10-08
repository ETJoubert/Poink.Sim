---
tags: [component, feature/marketchart, kind/component]
role: component
feature: MarketChart
feature_source: folder
kind: component
code_lines: 2
types: 1
methods: 0
fan_in: 4
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/MarketChart/Sparkline/MarketChartSparkline.razor
---
# MarketChartSparkline.razor

Standalone, read-only timeline of a bar list's close prices -- takes its own Bars directly (e.g. ProfilerPage's currently loaded/selected data) rather than syncing to a MarketChart instance, so it has no dependency on one being present, mounted, or interactive.

**Feature:** MarketChart · **Kind:** Razor component · **Code lines:** 2 · **Used by:** 4

## Types
- `MarketChartSparkline` — component, Razor component · empty

## Uses
- _nothing in Poink.Sim_
