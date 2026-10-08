---
tags: [helper, feature/simulation, kind/static-helper]
role: helper
feature: Simulation
feature_source: folder
kind: static-helper
code_lines: 32
types: 2
methods: 2
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Helpers/SimulationRowHelper.cs
---
# SimulationRowHelper.cs

Shapes a simulation's bars and ticks into the rows its charts and tick tables render.

**Feature:** Simulation · **Kind:** static helper · **Code lines:** 32 · **Used by:** 3

## Types
- `SimulationRowHelper` — class, static helper · 2 methods
- `TickRow` — record, nested, data type (at most one helper method) · 4 properties

## Uses
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
