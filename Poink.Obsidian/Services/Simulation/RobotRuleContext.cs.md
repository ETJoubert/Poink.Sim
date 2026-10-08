---
tags: [class]
source: Poink.Sim/Services/Simulation/RobotRuleContext.cs
---
# RobotRuleContext.cs

The rule-engine signals at one price, asked whether each of the robot's rule sets matches -- the engine-side counterpart of what the Simulation page's rule panels show. Market rows are built once per rule set and shared by every bot; position rows are added per bot, from that bot's own open position.

**Used by:** 2 files (see Backlinks)

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
