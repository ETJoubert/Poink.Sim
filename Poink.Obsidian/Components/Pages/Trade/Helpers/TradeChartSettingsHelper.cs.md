---
tags: [helper, feature/trade, kind/static-helper]
role: helper
feature: Trade
feature_source: folder
kind: static-helper
code_lines: 60
types: 1
methods: 3
fan_in: 1
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Helpers/TradeChartSettingsHelper.cs
---
# TradeChartSettingsHelper.cs

Maps the Trade chart panel's settings to and from the persisted page settings blob, including blobs saved before Indicators existed.

**Feature:** Trade · **Kind:** static helper · **Code lines:** 60 · **Used by:** 1

## Types
- `TradeChartSettingsHelper` — class, static helper · 3 methods

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/TradePageSettings.cs.md|TradePageSettings.cs]]
