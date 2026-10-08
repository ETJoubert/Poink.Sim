---
tags: [code-behind]
source: Poink.Sim/Components/Charts/TxFold/TxFold.razor.cs
---
# TxFold.razor.cs

Self-contained multi-timeframe comparison chart -- each toggled timeframe (see SnapshotTimeframeOptions, 1m/2m/4m/8m/15m/30m/1h/2h selected by default) renders as its own single bar over its own trailing window ending at AnchorEnd, side by side, via Compute over ticks fetched here.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Helpers/SplitTickReadHelper.cs.md|SplitTickReadHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[wwwroot/js/txFold.js.md|txFold.js]]
