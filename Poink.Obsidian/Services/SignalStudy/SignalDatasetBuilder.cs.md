---
tags: [class]
source: Poink.Sim/Services/SignalStudy/SignalDatasetBuilder.cs
---
# SignalDatasetBuilder.cs

Builds signal datasets with the same market feed and rule-engine rows the robot uses, sampling the first tick of every second. TxFold (loaded asynchronously) and position rows (which need a trade) are not part of a dataset.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Helpers/MarketRegimeHelper.cs.md|MarketRegimeHelper.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Helpers/RuleSignalRowsHelper.cs.md|RuleSignalRowsHelper.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
- [[Repository/Interfaces/ISignalDatasetRepository.cs.md|ISignalDatasetRepository.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/SignalStudy/ISignalDatasetBuilder.cs.md|ISignalDatasetBuilder.cs]]
- [[Services/Simulation/PrefetchingTickReader.cs.md|PrefetchingTickReader.cs]]
- [[Services/Simulation/RobotRuleContext.cs.md|RobotRuleContext.cs]]
- [[Services/Simulation/SimulationMarketFeed.cs.md|SimulationMarketFeed.cs]]
