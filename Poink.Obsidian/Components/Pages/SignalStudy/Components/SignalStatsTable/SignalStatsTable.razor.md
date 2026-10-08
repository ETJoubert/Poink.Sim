---
tags: [component, feature/signalstudy, kind/component]
role: component
feature: SignalStudy
feature_source: folder
kind: component
code_lines: 44
types: 1
methods: 1
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/SignalStudy/Components/SignalStatsTable/SignalStatsTable.razor
---
# SignalStatsTable.razor

Signal states (or pairs) with what followed their entries, at one forward horizon. Rows whose better side has a t-statistic of 2 or more and whose two halves agree are marked as worth a closer look.

**Feature:** SignalStudy · **Kind:** Razor component · **Code lines:** 44 · **Used by:** 3

## Types
- `SignalStatsTable` — component, Razor component · 1 method

## Uses
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
