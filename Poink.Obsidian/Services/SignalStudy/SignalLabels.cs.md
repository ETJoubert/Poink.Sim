---
tags: [class]
source: Poink.Sim/Services/SignalStudy/SignalLabels.cs
---
# SignalLabels.cs

What followed each point of a series: the mid-price move at every horizon, in ATRs, and the result of a stop/target trade opened there on each side, in R. Fills use the bid and ask, so the spread is paid; NaN marks a point whose outcome the data cannot show (no ATR yet, or the data ends first).

**Used by:** 2 files (see Backlinks)

## Uses
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
- [[Services/SignalStudy/SignalPointSeries.cs.md|SignalPointSeries.cs]]
