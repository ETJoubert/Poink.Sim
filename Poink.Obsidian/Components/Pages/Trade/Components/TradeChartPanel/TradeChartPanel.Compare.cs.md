---
tags: [class, feature/trade, kind/component-code]
role: class
feature: Trade
feature_source: folder
kind: component-code
code_lines: 131
types: 1
methods: 9
fan_in: 0
fan_out: 19
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/TradeChartPanel.Compare.cs
---
# TradeChartPanel.Compare.cs

The compare view that replaces the chart row while on: the charted symbol and every other watched symbol's live bars on one shared CompareSet, coloured like the watchlist so its cards are the legend. Every panel follows each tick: the % chart and XGrid from the bars, and at Offset 0 the TxFold from TradeLiveTickSource.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 131 · **Used by:** 0

## Types
- `TradeChartPanel` — class, class with behaviour · 9 methods, 12 properties, 9 fields

## Uses
- [[Components/Charts/CompareChart/CompareChartModels.cs.md|CompareChartModels.cs]]
- [[Components/Charts/CompareSet/CompareSetModels.cs.md|CompareSetModels.cs]]
- [[Components/Charts/CompareTxFold/CompareTxFold.razor.md|CompareTxFold.razor]]
- [[Components/Charts/CompareTxFold/ICompareTxFoldTickSource.cs.md|ICompareTxFoldTickSource.cs]]
- [[Components/Charts/CompareXGrid/CompareXGrid.razor.md|CompareXGrid.razor]]
- [[Components/Charts/CompareXGrid/CompareXGridModels.cs.md|CompareXGridModels.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartWindowHelper.cs.md|ChartWindowHelper.cs]]
- [[Components/Pages/Trade/Components/TradeChartPanel/TradeChartPanel.razor.md|TradeChartPanel.razor]]
- [[Components/Pages/Trade/Helpers/PositionPnlHelper.cs.md|PositionPnlHelper.cs]]
- [[Components/Pages/Trade/Helpers/TradeCompareHelper.cs.md|TradeCompareHelper.cs]]
- [[Components/Pages/Trade/Models/TradeChartModels.cs.md|TradeChartModels.cs]]
- [[Components/Pages/Trade/Services/TradeLiveTickSource.cs.md|TradeLiveTickSource.cs]]
- [[Components/Pages/Trade/Services/TradeSymbolSeries.cs.md|TradeSymbolSeries.cs]]
- [[Helpers/SymbolColorHelper.cs.md|SymbolColorHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/MarketData/History/TickHistoryPlanner.cs.md|TickHistoryPlanner.cs]]
