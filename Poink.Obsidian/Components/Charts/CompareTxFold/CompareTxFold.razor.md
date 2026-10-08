---
tags: [component]
source: Poink.Sim/Components/Charts/CompareTxFold/CompareTxFold.razor
---
# CompareTxFold.razor

TxFold for several symbols at once: one group per selected trailing timeframe ending at AnchorEnd, holding each symbol's bar as % change from that symbol's last price at the anchor, so differently priced symbols share one axis. Layout overlays the bars or sets them side by side. Draws via compareTxFold.js.

**Used by:** 8 files (see Backlinks)

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
