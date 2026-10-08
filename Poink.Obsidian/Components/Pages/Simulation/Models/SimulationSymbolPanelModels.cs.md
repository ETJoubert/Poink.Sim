---
tags: [model, feature/simulation, kind/data-bundle]
role: model
feature: Simulation
feature_source: folder
kind: data-bundle
code_lines: 40
types: 2
methods: 0
fan_in: 3
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Models/SimulationSymbolPanelModels.cs
---
# SimulationSymbolPanelModels.cs

Everything one watched symbol's chart set renders, already computed for the page's Lookback Window and Indicators: its chart bars and indicator lines, XGrid and its recent tick rate, volume/time profile samples and TxFold anchor.

**Feature:** Simulation · **Kind:** bundle of data types · **Code lines:** 40 · **Used by:** 3

## Types
- `SimulationSymbolPanelData` — record, data type (at most one helper method) · 22 properties
- `SimulationChartSetOptions` — record, data type (at most one helper method) · 8 properties

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
