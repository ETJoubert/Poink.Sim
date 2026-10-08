---
tags: [script, feature/comparetxfold, kind/script]
role: script
feature: CompareTxFold
feature_source: usage
kind: script
code_lines: 202
types: 0
methods: 0
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/wwwroot/js/compareTxFold.js
---
# compareTxFold.js

Canvas renderer for CompareTxFold.razor.cs: one group per trailing timeframe, each holding every symbol's bar as % change from that symbol's price at the anchor -- overlaid at partial opacity or side by side. Up bars are filled and down bars hollow, so direction reads in a single colour.

**Feature:** CompareTxFold (by usage) · **Kind:** script · **Code lines:** 202 · **Used by:** 1

## Uses
- [[wwwroot/js/chartColors.js.md|chartColors.js]]
- [[wwwroot/js/chartTooltip.js.md|chartTooltip.js]]
