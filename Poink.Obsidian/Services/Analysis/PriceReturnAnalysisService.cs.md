---
tags: [service, feature/analysis, kind/behavior]
role: service
feature: Analysis
feature_source: folder
kind: behavior
code_lines: 421
types: 2
methods: 18
fan_in: 1
fan_out: 9
summary_source: generated
candidates: []
source: Poink.Sim/Services/Analysis/PriceReturnAnalysisService.cs
---
# PriceReturnAnalysisService.cs

Service that handles price return analysis.

**Feature:** Analysis · **Kind:** class with behaviour · **Code lines:** 421 · **Used by:** 1

## Types
- `PriceReturnAnalysisService` — class, class with behaviour · 18 methods, 6 properties, 9 fields, 1 constructor, 1 event
- `OpenStart` — class, nested, data type (at most one helper method) · 6 fields

## Uses
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/PriceReturnModels.cs.md|PriceReturnModels.cs]]
- [[Models/QuartileBar.cs.md|QuartileBar.cs]]
- [[Models/TimeAboveBelowModels.cs.md|TimeAboveBelowModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Analysis/Interfaces/IPriceReturnAnalysisService.cs.md|IPriceReturnAnalysisService.cs]]
- [[Services/Simulation/QuartileBarAggregator.cs.md|QuartileBarAggregator.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
