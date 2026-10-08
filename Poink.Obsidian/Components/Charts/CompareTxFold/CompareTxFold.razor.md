---
tags: [component, feature/comparetxfold, kind/component]
role: component
feature: CompareTxFold
feature_source: folder
kind: component
code_lines: 58
types: 1
methods: 0
fan_in: 8
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareTxFold/CompareTxFold.razor
---
# CompareTxFold.razor

TxFold for several symbols at once: one group per selected trailing timeframe ending at AnchorEnd, holding each symbol's bar as % change from that symbol's last price at the anchor, so differently priced symbols share one axis. Layout overlays the bars or sets them side by side. Draws via compareTxFold.js.

**Feature:** CompareTxFold · **Kind:** Razor component · **Code lines:** 58 · **Used by:** 8

## Types
- `CompareTxFold` — component, Razor component · empty

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
