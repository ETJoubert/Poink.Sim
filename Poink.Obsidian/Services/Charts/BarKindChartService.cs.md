---
tags: [service, feature/charts, kind/mixed]
role: service
feature: Charts
feature_source: folder
kind: mixed
code_lines: 60
types: 2
methods: 4
fan_in: 3
fan_out: 8
summary_source: generated
candidates: []
source: Poink.Sim/Services/Charts/BarKindChartService.cs
---
# BarKindChartService.cs

Service that handles bar kind chart.

**Feature:** Charts · **Kind:** mixed types · **Code lines:** 60 · **Used by:** 3

## Types
- `IBarKindChartService` — interface, interface · 1 method
- `BarKindChartService` — class, class with behaviour · 3 methods, 2 fields, 1 constructor

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/QuartileBar.cs.md|QuartileBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Simulation/QuartileBarAggregator.cs.md|QuartileBarAggregator.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
