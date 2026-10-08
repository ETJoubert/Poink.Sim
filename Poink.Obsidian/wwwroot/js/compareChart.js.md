---
tags: [script, feature/comparechart, kind/script]
role: script
feature: CompareChart
feature_source: usage
kind: script
code_lines: 301
types: 0
methods: 0
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/wwwroot/js/compareChart.js
---
# compareChart.js

Multi-series comparison chart driven by CompareChart.razor.cs: one line or candle series per series on a shared time axis, an optional lower pane (pane index 1, e.g. a ratio line) and a shaded Lookback Window, plus caller-supplied zones and time markers. Per-instance state is keyed by the container element.

**Feature:** CompareChart (by usage) · **Kind:** script · **Code lines:** 301 · **Used by:** 1

## Uses
- [[wwwroot/js/chartColors.js.md|chartColors.js]]
- [[wwwroot/js/chartTooltip.js.md|chartTooltip.js]]
