---
tags: [code-behind, feature/txfold, kind/component-code]
role: code-behind
feature: TxFold
feature_source: folder
kind: component-code
code_lines: 245
types: 1
methods: 9
fan_in: 0
fan_out: 10
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/TxFold/TxFold.razor.cs
---
# TxFold.razor.cs

Self-contained multi-timeframe comparison chart -- each toggled timeframe (see SnapshotTimeframeOptions, 1m/2m/4m/8m/15m/30m/1h/2h selected by default) renders as its own single bar over its own trailing window ending at AnchorEnd, side by side, via Compute over ticks fetched here.

**Feature:** TxFold · **Kind:** component code-behind · **Code lines:** 245 · **Used by:** 0

## Types
- `TxFold` — class, Razor component · 9 methods, 29 properties, 23 fields

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
