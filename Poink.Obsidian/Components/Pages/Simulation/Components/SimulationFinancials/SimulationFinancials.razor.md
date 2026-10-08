---
tags: [component, feature/simulation, kind/component]
role: component
feature: Simulation
feature_source: folder
kind: component
code_lines: 59
types: 1
methods: 0
fan_in: 2
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationFinancials/SimulationFinancials.razor
---
# SimulationFinancials.razor

The Simulation page's Financials panel for one trader, fed straight from the simulation engine and re-rendered on its own, so balance, positions, history and statistics keep updating at max speed while the page does not render.

**Feature:** Simulation · **Kind:** Razor component · **Code lines:** 59 · **Used by:** 2

## Types
- `SimulationFinancials` — component, Razor component · empty

## Uses
- [[Components/Pages/Simulation/Components/SimulationEquityCurve/SimulationEquityCurve.razor.md|SimulationEquityCurve.razor]]
- [[Components/Shared/FinancialsPanel/FinancialsPanel.razor.md|FinancialsPanel.razor]]
- [[Components/Shared/PositionTable/PositionTable.razor.md|PositionTable.razor]]
- [[Components/Shared/StatisticsPanel/StatisticsPanel.razor.md|StatisticsPanel.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
