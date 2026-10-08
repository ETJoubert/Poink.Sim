---
tags: [code-behind]
source: Poink.Sim/Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs
---
# TickVolumeProfileChart.razor.cs

Horizontal-bar tick histogram (price on the row axis, tick count as bar width), deliberately plain Razor/CSS rather than the vendored lightweight-charts library, since it just shares OhlcvChart's "high price at top" orientation without being pixel-synced to its zoom/pan.

**Used by:** 25 files (see Backlinks)

## Uses
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.md|TickVolumeProfileChart.razor]]
- [[Components/Charts/TickVolumeProfileChart/VolumeProfileStats.cs.md|VolumeProfileStats.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
