---
tags: [helper]
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/ChartWindowHelper.cs
---
# ChartWindowHelper.cs

Pure lookback-window math for TradeChartPanel -- windowing, position/draft lines and XGrid position mapping, all derived from the same offset/width slider (see TradeChartPanel.razor.cs's ClampedBarWindow/ClampedBarOffset). Internal because it takes BarPriceHistogram, which is itself internal.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Display/XGridPosition.cs.md|XGridPosition.cs]]
- [[Components/Pages/Trade/TradeDraftModels.cs.md|TradeDraftModels.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
