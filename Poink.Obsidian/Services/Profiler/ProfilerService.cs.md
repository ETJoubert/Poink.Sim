---
tags: [service, feature/profiler, kind/single-method, candidate/single-method-service]
role: service
feature: Profiler
feature_source: folder
kind: single-method
code_lines: 67
types: 1
methods: 1
fan_in: 3
fan_out: 8
summary_source: generated
candidates: [single-method-service]
source: Poink.Sim/Services/Profiler/ProfilerService.cs
---
# ProfilerService.cs

Service that handles profiler.

**Feature:** Profiler · **Kind:** single-method class · **Code lines:** 67 · **Used by:** 3

## Types
- `ProfilerService` — class, single-method class · 1 method, 3 fields, 1 constructor

## Candidates
- **single-method-service** — Only `LoadAsync` behind `IProfilerService`, its sole implementation; fold into a broader service unless it is a deliberate seam.

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/ProfilerModels.cs.md|ProfilerModels.cs]]
- [[Models/QuartileBar.cs.md|QuartileBar.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Profiler/IProfilerService.cs.md|IProfilerService.cs]]
- [[Services/Simulation/QuartileBarAggregator.cs.md|QuartileBarAggregator.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
