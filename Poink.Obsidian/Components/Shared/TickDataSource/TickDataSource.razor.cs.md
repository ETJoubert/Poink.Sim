---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 66
types: 1
methods: 6
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/TickDataSource/TickDataSource.razor.cs
---
# TickDataSource.razor.cs

Symbol picker + date-range slider for a ticks_&lt;symbol&gt; table, shared by SimulationPage and TrainPage so both pages pick historic data the same way.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 66 · **Used by:** 0

## Types
- `TickDataSource` — class, Razor component · 6 methods, 12 properties, 3 fields

## Uses
- [[Components/Shared/TickDataSource/TickDataSource.razor.md|TickDataSource.razor]]
- [[Helpers/TickTableRangeHelper.cs.md|TickTableRangeHelper.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
