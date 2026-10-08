---
tags: [component, feature/tickvolumeprofilechart, kind/component]
role: component
feature: TickVolumeProfileChart
feature_source: folder
kind: component
code_lines: 66
types: 1
methods: 0
fan_in: 7
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor
---
# TickVolumeProfileChart.razor

Horizontal-bar tick histogram (price on the row axis, tick count as bar width), deliberately plain Razor/CSS rather than the vendored lightweight-charts library, since it just shares OhlcvChart's "high price at top" orientation without being pixel-synced to its zoom/pan.

**Feature:** TickVolumeProfileChart · **Kind:** Razor component · **Code lines:** 66 · **Used by:** 7

## Types
- `TickVolumeProfileChart` — component, Razor component · empty

## Uses
- _nothing in Poink.Sim_
