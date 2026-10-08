---
tags: [code-behind, feature/simulation, kind/component-code]
role: code-behind
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 96
types: 1
methods: 3
fan_in: 0
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationSymbolChartSet/SimulationSymbolChartSet.razor.cs
---
# SimulationSymbolChartSet.razor.cs

One watched symbol's full chart set on the Simulation page -- main chart, XGrid, volume/time profiles and TxFold -- shown by the page's panel toggles and driven by its Lookback Window and Indicators, like a Profiler chart set.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 96 · **Used by:** 0

## Types
- `SimulationSymbolChartSet` — class, Razor component · 3 methods, 12 properties, 12 fields

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Pages/Simulation/Components/SimulationSymbolChartSet/SimulationSymbolChartSet.razor.md|SimulationSymbolChartSet.razor]]
- [[Components/Pages/Simulation/Models/SimulationSymbolPanelModels.cs.md|SimulationSymbolPanelModels.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsHelper.cs.md|IndicatorsHelper.cs]]
- [[Helpers/ChartSetDividerScript.cs.md|ChartSetDividerScript.cs]]
