---
tags: [class, feature/signalstudy, kind/behavior]
role: class
feature: SignalStudy
feature_source: folder
kind: behavior
code_lines: 85
types: 1
methods: 3
fan_in: 2
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/SignalStudy/SignalLabels.cs
---
# SignalLabels.cs

What followed each point of a series: the mid-price move at every horizon, in ATRs, and the result of a stop/target trade opened there on each side, in R. Fills use the bid and ask, so the spread is paid; NaN marks a point whose outcome the data cannot show (no ATR yet, or the data ends first).

**Feature:** SignalStudy · **Kind:** class with behaviour · **Code lines:** 85 · **Used by:** 2

## Types
- `SignalLabels` — class, class with behaviour · 3 methods, 5 properties, 1 field, 1 constructor

## Uses
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
- [[Services/SignalStudy/SignalPointSeries.cs.md|SignalPointSeries.cs]]
