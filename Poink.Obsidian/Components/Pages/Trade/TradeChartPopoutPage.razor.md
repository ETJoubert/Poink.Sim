---
tags: [page, feature/trade, kind/page]
role: page
feature: Trade
feature_source: folder
kind: page
code_lines: 6
types: 1
methods: 0
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/TradeChartPopoutPage.razor
---
# TradeChartPopoutPage.razor

Bare host for a TradeChartPanel popped out into its own OS window via PopoutButton -- no page chrome (see PopoutLayout), initial state read entirely from the query string ChartPopoutUrlHelper built on the panel it was popped out from.

**Feature:** Trade · **Kind:** Razor page · **Code lines:** 6 · **Used by:** 1

## Types
- `TradeChartPopoutPage` — component, Razor component · empty

## Uses
- [[Components/Layout/PopoutLayout.razor.md|PopoutLayout.razor]]
- [[Components/Pages/Trade/Components/TradeChartPanel/TradeChartPanel.razor.md|TradeChartPanel.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
