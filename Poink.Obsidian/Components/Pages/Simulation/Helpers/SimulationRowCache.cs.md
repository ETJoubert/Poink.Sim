---
tags: [class, feature/simulation, kind/behavior]
role: class
feature: Simulation
feature_source: folder
kind: behavior
code_lines: 27
types: 2
methods: 3
fan_in: 5
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Helpers/SimulationRowCache.cs
---
# SimulationRowCache.cs

Each symbol's chart and tick-table rows, rebuilt only when the engine publishes that symbol's bars or ticks anew, so an unchanged symbol costs no conversion per snapshot and keeps handing out the same list.

**Feature:** Simulation · **Kind:** class with behaviour · **Code lines:** 27 · **Used by:** 5

## Types
- `SimulationRowCache` — class, class with behaviour · 3 methods, 1 field
- `Entry` — class, nested, data type (at most one helper method) · 4 fields

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationRowHelper.cs.md|SimulationRowHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
