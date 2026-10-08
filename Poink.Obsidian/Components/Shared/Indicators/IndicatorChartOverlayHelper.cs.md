---
tags: [helper, feature/shared, kind/mixed]
role: helper
feature: Shared
feature_source: shared
kind: mixed
code_lines: 73
types: 2
methods: 6
fan_in: 4
fan_out: 8
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/Indicators/IndicatorChartOverlayHelper.cs
---
# IndicatorChartOverlayHelper.cs

Builds the market chart's indicator lines so every host draws them the same way: price levels from the window's merged tick samples, VWAP and the regression fit from the window's bars, and day/interval lines across every loaded bar.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 73 · **Used by:** 4

## Types
- `IndicatorChartOverlays` — record, data type (at most one helper method) · 5 properties
- `IndicatorChartOverlayHelper` — class, static helper · 6 methods

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/VolumeProfileStats.cs.md|VolumeProfileStats.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Helpers/VectorStatsHelper.cs.md|VectorStatsHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
