---
tags: [class]
source: Poink.Sim/Helpers/TxFoldBarsCache.cs
---
# TxFoldBarsCache.cs

Loads and caches the TxFold mean/stddev bars the rule engine's TxFold rows read, refetching only when the anchor, range or selected timeframes change -- shared by the rule panel and the robot so neither relies on a chart having already loaded the data.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
