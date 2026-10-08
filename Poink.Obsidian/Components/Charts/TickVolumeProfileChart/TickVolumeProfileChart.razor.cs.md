---
tags: [code-behind, feature/tickvolumeprofilechart, kind/component-code]
role: code-behind
feature: TickVolumeProfileChart
feature_source: folder
kind: component-code
code_lines: 115
types: 3
methods: 3
fan_in: 20
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs
---
# TickVolumeProfileChart.razor.cs

Horizontal-bar tick histogram (price on the row axis, tick count as bar width), deliberately plain Razor/CSS rather than the vendored lightweight-charts library, since it just shares OhlcvChart's "high price at top" orientation without being pixel-synced to its zoom/pan.

**Feature:** TickVolumeProfileChart · **Kind:** component code-behind · **Code lines:** 115 · **Used by:** 20

## Types
- `WeightedPriceSample` — record struct, data type (at most one helper method) · 4 properties
- `TickVolumeProfileChart` — class, Razor component · 3 methods, 8 properties, 5 fields
- `Bucket` — record, nested, data type (at most one helper method) · 12 properties

## Uses
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.md|TickVolumeProfileChart.razor]]
- [[Components/Charts/TickVolumeProfileChart/VolumeProfileStats.cs.md|VolumeProfileStats.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
