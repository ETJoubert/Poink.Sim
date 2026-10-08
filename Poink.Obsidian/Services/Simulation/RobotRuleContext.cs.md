---
tags: [class, feature/simulation, kind/mixed]
role: class
feature: Simulation
feature_source: folder
kind: mixed
code_lines: 117
types: 2
methods: 5
fan_in: 2
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Services/Simulation/RobotRuleContext.cs
---
# RobotRuleContext.cs

The rule-engine signals at one price, asked whether each of the robot's rule sets matches -- the engine-side counterpart of what the Simulation page's rule panels show. Market rows are built once per rule set and shared by every bot; position rows are added per bot, from that bot's own open position.

**Feature:** Simulation · **Kind:** mixed types · **Code lines:** 117 · **Used by:** 2

## Types
- `RobotBarData` — class, class with behaviour · 2 methods, 5 properties
- `RobotRuleContext` — class, class with behaviour · 3 methods, 5 fields, 1 constructor

## Uses
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Helpers/RuleSignalRowsHelper.cs.md|RuleSignalRowsHelper.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
