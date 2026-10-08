---
tags: [settings, feature/shared, kind/data]
role: settings
feature: Shared
feature_source: shared
kind: data
code_lines: 44
types: 1
methods: 0
fan_in: 2
fan_out: 10
summary_source: documented
candidates: []
source: Poink.Sim/Models/SimulationPageSettings.cs
---
# SimulationPageSettings.cs

Snapshot of the Simulation page's setup form (Data Range/Trader Account/Risk Policies), persisted to browser localStorage so it survives navigating away and back -- the page's own fields are reset to their defaults every time Blazor recreates the component.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 44 · **Used by:** 2

## Types
- `SimulationPageSettings` — record, data type (at most one helper method) · 34 properties, 1 field

## Uses
- [[Components/Charts/CompareChart/CompareChartModels.cs.md|CompareChartModels.cs]]
- [[Components/Charts/CompareSet/CompareSetModels.cs.md|CompareSetModels.cs]]
- [[Components/Charts/CompareTxFold/CompareTxFold.razor.md|CompareTxFold.razor]]
- [[Components/Charts/CompareXGrid/CompareXGrid.razor.md|CompareXGrid.razor]]
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Components/Shared/TradeConsole/TradeConsoleModels.cs.md|TradeConsoleModels.cs]]
- [[Models/RobotPanelModels.cs.md|RobotPanelModels.cs]]
- [[Models/RobotPositionRules.cs.md|RobotPositionRules.cs]]
- [[Models/RobotRuleSet.cs.md|RobotRuleSet.cs]]
- [[Models/RuleSignalSettings.cs.md|RuleSignalSettings.cs]]
