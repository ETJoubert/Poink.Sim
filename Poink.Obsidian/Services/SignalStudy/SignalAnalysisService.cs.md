---
tags: [service, feature/signalstudy, kind/behavior]
role: service
feature: SignalStudy
feature_source: folder
kind: behavior
code_lines: 82
types: 1
methods: 5
fan_in: 1
fan_out: 7
summary_source: interface-doc
candidates: []
source: Poink.Sim/Services/SignalStudy/SignalAnalysisService.cs
---
# SignalAnalysisService.cs

Labels a signal dataset with what followed each point and ranks the signal states, then the best pairs of them.

**Feature:** SignalStudy · **Kind:** class with behaviour · **Code lines:** 82 · **Used by:** 1

## Types
- `SignalAnalysisService` — class, class with behaviour · 5 methods, 4 fields, 1 constructor

## Uses
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
- [[Repository/Interfaces/ISignalDatasetRepository.cs.md|ISignalDatasetRepository.cs]]
- [[Services/SignalStudy/ISignalAnalysisService.cs.md|ISignalAnalysisService.cs]]
- [[Services/SignalStudy/SignalLabels.cs.md|SignalLabels.cs]]
- [[Services/SignalStudy/SignalPointSeries.cs.md|SignalPointSeries.cs]]
- [[Services/SignalStudy/SignalStateAnalyzer.cs.md|SignalStateAnalyzer.cs]]
