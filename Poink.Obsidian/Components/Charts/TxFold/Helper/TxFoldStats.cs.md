---
tags: [class]
source: Poink.Sim/Components/Charts/TxFold/Helper/TxFoldStats.cs
---
# TxFoldStats.cs

Levels is null only when there are no ticks at all -- unlike Poc/Vah/Val inside it, which can independently be null just from too little data for a meaningful value area.

**Used by:** 7 files (see Backlinks)

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Helpers/BarAggregator.cs.md|BarAggregator.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
