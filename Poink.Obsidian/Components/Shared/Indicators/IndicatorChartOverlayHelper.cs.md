---
tags: [helper]
source: Poink.Sim/Components/Shared/Indicators/IndicatorChartOverlayHelper.cs
---
# IndicatorChartOverlayHelper.cs

Builds the market chart's indicator lines so every host draws them the same way: price levels from the window's merged tick samples, VWAP and the regression fit from the window's bars, and day/interval lines across every loaded bar.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/VolumeProfileStats.cs.md|VolumeProfileStats.cs]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Helpers/VectorStatsHelper.cs.md|VectorStatsHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
