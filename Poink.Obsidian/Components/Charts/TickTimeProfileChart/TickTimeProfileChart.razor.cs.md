---
tags: [code-behind, feature/ticktimeprofilechart, kind/component-code]
role: code-behind
feature: TickTimeProfileChart
feature_source: folder
kind: component-code
code_lines: 142
types: 4
methods: 5
fan_in: 15
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs
---
# TickTimeProfileChart.razor.cs

Horizontal-bar time histogram, same "price on the row axis" layout and POC/Value Area readout as TickVolumeProfileChart, but each bar's width is how long price dwelled in that bucket rather than how many ticks landed there.

**Feature:** TickTimeProfileChart · **Kind:** component code-behind · **Code lines:** 142 · **Used by:** 15

## Types
- `TimedPrice` — record struct, data type (at most one helper method) · 2 properties
- `WeightedTimeSample` — record struct, data type (at most one helper method) · 5 properties
- `TickTimeProfileChart` — class, Razor component · 5 methods, 8 properties, 5 fields
- `Bucket` — record, nested, data type (at most one helper method) · 12 properties

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.md|TickTimeProfileChart.razor]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Helpers/TimeProfileHelper.cs.md|TimeProfileHelper.cs]]
