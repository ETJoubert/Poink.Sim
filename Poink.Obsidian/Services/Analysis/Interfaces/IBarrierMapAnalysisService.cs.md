---
tags: [interface, feature/analysis, kind/interface]
role: interface
feature: Analysis
feature_source: folder
kind: interface
code_lines: 5
types: 1
methods: 1
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Analysis/Interfaces/IBarrierMapAnalysisService.cs
---
# IBarrierMapAnalysisService.cs

Runs barrier maps in the background: a trade every few seconds through a range, tested against a grid of stop-loss and take-profit sizes and time limits. One run at a time.

**Feature:** Analysis · **Kind:** interface · **Code lines:** 5 · **Used by:** 4

## Types
- `IBarrierMapAnalysisService` — interface, interface · 1 method, 1 property

## Uses
- [[Models/BarrierMapModels.cs.md|BarrierMapModels.cs]]
- [[Services/Analysis/Interfaces/IBackgroundAnalysisService.cs.md|IBackgroundAnalysisService.cs]]
