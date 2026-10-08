---
tags: [helper, feature/trade, kind/static-helper]
role: helper
feature: Trade
feature_source: folder
kind: static-helper
code_lines: 20
types: 1
methods: 2
fan_in: 2
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/ChartPopoutUrlHelper.cs
---
# ChartPopoutUrlHelper.cs

Builds the query string TradeChartPopoutPage reads back via [SupplyParameterFromQuery], so a popped-out chart opens already showing the same symbol, bar size, and panel settings as the panel it was popped out from. The settings travel as one JSON value so new panel options need no new query keys.

**Feature:** Trade · **Kind:** static helper · **Code lines:** 20 · **Used by:** 2

## Types
- `ChartPopoutUrlHelper` — class, static helper · 2 methods

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
