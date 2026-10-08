---
tags: [component, feature/shared, kind/component]
role: component
feature: Shared
feature_source: shared
kind: component
code_lines: 27
types: 1
methods: 0
fan_in: 7
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/TickDataSource/TickDataSource.razor
---
# TickDataSource.razor

Symbol picker + date-range slider for a ticks_&lt;symbol&gt; table, shared by SimulationPage and TrainPage so both pages pick historic data the same way.

**Feature:** Shared · **Kind:** Razor component · **Code lines:** 27 · **Used by:** 7

## Types
- `TickDataSource` — component, Razor component · empty

## Uses
- [[Components/Shared/DateRangeSlider/DateRangeSlider.razor.md|DateRangeSlider.razor]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
