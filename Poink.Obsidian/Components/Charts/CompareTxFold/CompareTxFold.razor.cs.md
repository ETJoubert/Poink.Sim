---
tags: [code-behind]
source: Poink.Sim/Components/Charts/CompareTxFold/CompareTxFold.razor.cs
---
# CompareTxFold.razor.cs

TxFold for several symbols at once: one group per selected trailing timeframe ending at AnchorEnd, holding each symbol's bar as % change from that symbol's last price at the anchor, so differently priced symbols share one axis. Layout overlays the bars or sets them side by side. Draws via compareTxFold.js.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFold.razor.md|CompareTxFold.razor]]
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Components/Charts/CompareTxFold/ICompareTxFoldTickSource.cs.md|ICompareTxFoldTickSource.cs]]
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Helpers/SplitTickReadHelper.cs.md|SplitTickReadHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[wwwroot/js/compareTxFold.js.md|compareTxFold.js]]
