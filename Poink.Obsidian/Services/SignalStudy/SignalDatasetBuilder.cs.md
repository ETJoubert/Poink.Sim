---
tags: [class, feature/signalstudy, kind/behavior]
role: class
feature: SignalStudy
feature_source: folder
kind: behavior
code_lines: 216
types: 3
methods: 9
fan_in: 1
fan_out: 12
summary_source: documented
candidates: []
source: Poink.Sim/Services/SignalStudy/SignalDatasetBuilder.cs
---
# SignalDatasetBuilder.cs

Builds signal datasets with the same market feed and rule-engine rows the robot uses, sampling the first tick of every second. TxFold (loaded asynchronously) and position rows (which need a trade) are not part of a dataset.

**Feature:** SignalStudy · **Kind:** class with behaviour · **Code lines:** 216 · **Used by:** 1

## Types
- `SignalDatasetBuilder` — class, class with behaviour · 6 methods, 4 properties, 10 fields, 1 constructor, 1 event
- `SampledBarData` — class, nested, single-method class · 1 method, 1 property, 4 fields, 1 constructor
- `SignalCatalog` — class, nested, class with behaviour · 2 methods, 2 properties, 3 fields, 1 constructor

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
