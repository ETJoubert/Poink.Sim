---
tags: [component, feature/simulation, kind/component]
role: component
feature: Simulation
feature_source: folder
kind: component
code_lines: 74
types: 1
methods: 0
fan_in: 3
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationSymbolChartSet/SimulationSymbolChartSet.razor
---
# SimulationSymbolChartSet.razor

One watched symbol's full chart set on the Simulation page -- main chart, XGrid, volume/time profiles and TxFold -- shown by the page's panel toggles and driven by its Lookback Window and Indicators, like a Profiler chart set.

**Feature:** Simulation · **Kind:** Razor component · **Code lines:** 74 · **Used by:** 3

## Types
- `SimulationSymbolChartSet` — component, Razor component · empty

## Uses
- [[Components/Charts/MarketChart/Container/MarketChartContainer.razor.md|MarketChartContainer.razor]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.md|TickTimeProfileChart.razor]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.md|TickVolumeProfileChart.razor]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Components/Charts/XGrid/Controller/XGridController.razor.md|XGridController.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/ViewDatatable/ViewDatatable.razor.md|ViewDatatable.razor]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
