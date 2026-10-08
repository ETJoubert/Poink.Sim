---
tags: [component, feature/txfold, kind/component]
role: component
feature: TxFold
feature_source: folder
kind: component
code_lines: 58
types: 1
methods: 0
fan_in: 20
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/TxFold/TxFold.razor
---
# TxFold.razor

Self-contained multi-timeframe comparison chart -- each toggled timeframe (see SnapshotTimeframeOptions, 1m/2m/4m/8m/15m/30m/1h/2h selected by default) renders as its own single bar over its own trailing window ending at AnchorEnd, side by side, via Compute over ticks fetched here.

**Feature:** TxFold · **Kind:** Razor component · **Code lines:** 58 · **Used by:** 20

## Types
- `TxFold` — component, Razor component · empty

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
