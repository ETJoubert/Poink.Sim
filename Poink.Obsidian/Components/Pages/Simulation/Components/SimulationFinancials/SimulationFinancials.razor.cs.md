---
tags: [code-behind, feature/simulation, kind/component-code]
role: code-behind
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 129
types: 2
methods: 6
fan_in: 0
fan_out: 12
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationFinancials/SimulationFinancials.razor.cs
---
# SimulationFinancials.razor.cs

The Simulation page's Financials panel for one trader, fed straight from the simulation engine and re-rendered on its own, so balance, positions, history and statistics keep updating at max speed while the page does not render.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 129 · **Used by:** 0

## Types
- `SimulationFinancials` — class, Razor component · 6 methods, 9 properties, 2 fields
- `DecisionLogRow` — record, nested, data type (at most one helper method) · 8 properties

## Uses
- [[Components/Pages/Simulation/Components/SimulationFinancials/SimulationFinancials.razor.md|SimulationFinancials.razor]]
- [[Components/Shared/PositionTable/PositionRowFormat.cs.md|PositionRowFormat.cs]]
- [[Components/Shared/PositionTable/PositionTable.razor.md|PositionTable.razor]]
- [[Components/Shared/PositionTable/PositionTableModels.cs.md|PositionTableModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationFinancialsModels.cs.md|SimulationFinancialsModels.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Models/StatGroup.cs.md|StatGroup.cs]]
- [[Models/TradeStatistics.cs.md|TradeStatistics.cs]]
- [[Services/Simulation/ISimulationEngine.cs.md|ISimulationEngine.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
