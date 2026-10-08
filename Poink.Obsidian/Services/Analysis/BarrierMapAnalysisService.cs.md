---
tags: [service, feature/analysis, kind/behavior]
role: service
feature: Analysis
feature_source: folder
kind: behavior
code_lines: 119
types: 1
methods: 5
fan_in: 1
fan_out: 4
summary_source: interface-doc
candidates: []
source: Poink.Sim/Services/Analysis/BarrierMapAnalysisService.cs
---
# BarrierMapAnalysisService.cs

Runs barrier maps in the background: a trade every few seconds through a range, tested against a grid of stop-loss and take-profit sizes and time limits. One run at a time.

**Feature:** Analysis · **Kind:** class with behaviour · **Code lines:** 119 · **Used by:** 1

## Types
- `BarrierMapAnalysisService` — class, class with behaviour · 5 methods, 6 properties, 6 fields, 1 constructor, 1 event

## Uses
- [[Models/BarrierMapModels.cs.md|BarrierMapModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Analysis/BarrierMapCalculator.cs.md|BarrierMapCalculator.cs]]
- [[Services/Analysis/Interfaces/IBarrierMapAnalysisService.cs.md|IBarrierMapAnalysisService.cs]]
