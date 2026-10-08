---
tags: [code-behind, feature/profiler, kind/component-code]
role: code-behind
feature: Profiler
feature_source: folder
kind: component-code
code_lines: 68
types: 1
methods: 10
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Profiler/Components/ProfilerSymbolPanel/ProfilerSymbolPanel.razor.cs
---
# ProfilerSymbolPanel.razor.cs

The Profiler page's left side panel: lists every ticks_&lt;symbol&gt; table in the database and keeps an ordered selection whose first entry is the primary symbol, the one whose chart set carries the page's drawings.

**Feature:** Profiler · **Kind:** component code-behind · **Code lines:** 68 · **Used by:** 0

## Types
- `ProfilerSymbolPanel` — class, Razor component · 10 methods, 10 properties, 5 fields

## Uses
- [[Components/Pages/Profiler/Components/ProfilerSymbolPanel/ProfilerSymbolPanel.razor.md|ProfilerSymbolPanel.razor]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
