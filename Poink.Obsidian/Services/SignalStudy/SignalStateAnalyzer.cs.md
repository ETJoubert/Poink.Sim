---
tags: [class, feature/signalstudy, kind/mixed]
role: class
feature: SignalStudy
feature_source: folder
kind: mixed
code_lines: 116
types: 3
methods: 8
fan_in: 1
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/SignalStudy/SignalStateAnalyzer.cs
---
# SignalStateAnalyzer.cs

Measures what followed the entries into signal states -- the moments a state becomes true -- against the same labels for every point, which is what entering at a random second would give.

**Feature:** SignalStudy · **Kind:** mixed types · **Code lines:** 116 · **Used by:** 1

## Types
- `SignalStateRef` — record struct, data type (at most one helper method) · 1 method, 4 properties
- `SignalStateAnalyzer` — class, class with behaviour · 5 methods, 8 fields, 1 constructor
- `Accumulator` — class, nested, class with behaviour · 2 methods, 4 properties, 2 fields

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationRowCache.cs.md|SimulationRowCache.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
- [[Services/SignalStudy/SignalLabels.cs.md|SignalLabels.cs]]
- [[Services/SignalStudy/SignalPointSeries.cs.md|SignalPointSeries.cs]]
