---
tags: [helper, feature/profiler, kind/static-helper]
role: helper
feature: Profiler
feature_source: folder
kind: static-helper
code_lines: 29
types: 1
methods: 4
fan_in: 2
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Profiler/Helpers/ProfilerSymbolStatsHelper.cs
---
# ProfilerSymbolStatsHelper.cs

Profile samples and windowed bars for one ProfilerSymbolData, narrowed to the Lookback Window when one is set and spanning every loaded bar otherwise.

**Feature:** Profiler · **Kind:** static helper · **Code lines:** 29 · **Used by:** 2

## Types
- `ProfilerSymbolStatsHelper` — class, static helper · 4 methods

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Pages/Profiler/Models/ProfilerSymbolData.cs.md|ProfilerSymbolData.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
