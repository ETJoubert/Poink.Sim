---
tags: [helper, feature/trade, kind/static-helper]
role: helper
feature: Trade
feature_source: folder
kind: static-helper
code_lines: 95
types: 1
methods: 9
fan_in: 2
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/ChartWindowHelper.cs
---
# ChartWindowHelper.cs

Pure lookback-window math for TradeChartPanel -- windowing, position/draft lines and XGrid position mapping, all derived from the same offset/width slider (see TradeChartPanel.razor.cs's ClampedBarWindow/ClampedBarOffset). Internal because it takes BarPriceHistogram, which is itself internal.

**Feature:** Trade · **Kind:** static helper · **Code lines:** 95 · **Used by:** 2

## Types
- `ChartWindowHelper` — class, static helper · 9 methods

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Display/XGridPosition.cs.md|XGridPosition.cs]]
- [[Components/Pages/Trade/TradeDraftModels.cs.md|TradeDraftModels.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
