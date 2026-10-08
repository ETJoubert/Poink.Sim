---
tags: [code-behind, feature/comparetxfold, kind/component-code]
role: code-behind
feature: CompareTxFold
feature_source: folder
kind: component-code
code_lines: 181
types: 2
methods: 13
fan_in: 0
fan_out: 10
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareTxFold/CompareTxFold.razor.cs
---
# CompareTxFold.razor.cs

TxFold for several symbols at once: one group per selected trailing timeframe ending at AnchorEnd, holding each symbol's bar as % change from that symbol's last price at the anchor, so differently priced symbols share one axis. Layout overlays the bars or sets them side by side. Draws via compareTxFold.js.

**Feature:** CompareTxFold · **Kind:** component code-behind · **Code lines:** 181 · **Used by:** 0

## Types
- `CompareTxFold` — class, Razor component · 13 methods, 18 properties, 8 fields
- `SymbolSnapshot` — record, nested, data type (at most one helper method) · 4 properties

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
