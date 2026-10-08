---
tags: [settings, feature/trade, kind/data]
role: settings
feature: Trade
feature_source: folder
kind: data
code_lines: 36
types: 1
methods: 0
fan_in: 6
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs
---
# ChartPanelSettings.cs

TradeChartPanel's persisted toggle/window/XGrid/indicator/compare state -- round-tripped through TradePage's localStorage settings blob via CurrentSettings/InitialSettings, and through the popout URL.

**Feature:** Trade · **Kind:** data type (at most one helper method) · **Code lines:** 36 · **Used by:** 6

## Types
- `ChartPanelSettings` — record, data type (at most one helper method) · 14 properties

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
- [[Components/Pages/Trade/Models/TradeChartModels.cs.md|TradeChartModels.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
