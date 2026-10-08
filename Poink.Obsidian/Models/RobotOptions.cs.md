---
tags: [settings, feature/shared, kind/mixed]
role: settings
feature: Shared
feature_source: shared
kind: mixed
code_lines: 36
types: 2
methods: 3
fan_in: 7
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Models/RobotOptions.cs
---
# RobotOptions.cs

Everything the robot needs to act on its own, pushed to the simulation engine whenever the user changes any of it -- the on/off switch, how positions are sized, the open/close conditions, and the window settings those conditions are evaluated over, and how often (in simulated seconds) the bots look at them.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 36 · **Used by:** 7

## Types
- `RobotRules` — record, data type (at most one helper method) · 5 properties
- `RobotOptions` — record, class with behaviour · 3 methods, 18 properties

## Uses
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Components/Shared/TradeConsole/TradeConsoleModels.cs.md|TradeConsoleModels.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Models/RobotPositionRules.cs.md|RobotPositionRules.cs]]
- [[Models/RuleSignalSettings.cs.md|RuleSignalSettings.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
