---
tags: [code-behind, feature/trade, kind/component-code]
role: code-behind
feature: Trade
feature_source: folder
kind: component-code
code_lines: 12
types: 1
methods: 0
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/TradeChartPopoutPage.razor.cs
---
# TradeChartPopoutPage.razor.cs

Bare host for a TradeChartPanel popped out into its own OS window via PopoutButton -- no page chrome (see PopoutLayout), initial state read entirely from the query string ChartPopoutUrlHelper built on the panel it was popped out from.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 12 · **Used by:** 0

## Types
- `TradeChartPopoutPage` — class, Razor component · 5 properties

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPopoutUrlHelper.cs.md|ChartPopoutUrlHelper.cs]]
- [[Components/Pages/Trade/TradeChartPopoutPage.razor.md|TradeChartPopoutPage.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
