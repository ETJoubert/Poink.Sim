---
tags: [helper]
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/ChartPopoutUrlHelper.cs
---
# ChartPopoutUrlHelper.cs

Builds the query string TradeChartPopoutPage reads back via [SupplyParameterFromQuery], so a popped-out chart opens already showing the same symbol, bar size, and panel settings as the panel it was popped out from. The settings travel as one JSON value so new panel options need no new query keys.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
