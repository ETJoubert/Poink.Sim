---
tags: [component, feature/trade, kind/component]
role: component
feature: Trade
feature_source: folder
kind: component
code_lines: 130
types: 1
methods: 0
fan_in: 6
fan_out: 18
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/TradeChartPanel.razor
---
# TradeChartPanel.razor

Charts region of the live Trade page -- toolbar, Indicators with the lookback window, and the main chart/XGrid/volume-profile/time-profile/TxFold panels for whichever symbol TradePage tells it to show, or the compare view across the watchlist.

**Feature:** Trade · **Kind:** Razor component · **Code lines:** 130 · **Used by:** 6

## Types
- `TradeChartPanel` — component, Razor component · empty

## Uses
- [[Components/Charts/CompareSet/CompareSet.razor.md|CompareSet.razor]]
- [[Components/Charts/CompareTxFold/CompareTxFold.razor.md|CompareTxFold.razor]]
- [[Components/Charts/CompareXGrid/CompareXGrid.razor.md|CompareXGrid.razor]]
- [[Components/Charts/DataSeriesSyncChart/DataSeriesSyncChart.razor.md|DataSeriesSyncChart.razor]]
- [[Components/Charts/MarketChart/Container/MarketChartContainer.razor.md|MarketChartContainer.razor]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.md|TickTimeProfileChart.razor]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.md|TickVolumeProfileChart.razor]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Components/Charts/XGrid/Controller/XGridController.razor.md|XGridController.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Shared/BarSizeSelect/BarSizeSelect.razor.md|BarSizeSelect.razor]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsHelper.cs.md|IndicatorsHelper.cs]]
- [[Components/Shared/LookbackWindowBar/LookbackWindowBar.razor.md|LookbackWindowBar.razor]]
- [[Components/Shared/PopoutButton/PopoutButton.razor.md|PopoutButton.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
