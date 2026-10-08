---
tags: [model, feature/simulation, kind/data, candidate/move-to-feature]
role: model
feature: Simulation
feature_source: usage
kind: data
code_lines: 12
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/SimulationFinancialsModels.cs
---
# SimulationFinancialsModels.cs

Every trader's account figures and the quote they were valued at -- published at max speed too, when no full snapshot is, so the Financials panel can keep updating on its own.

**Feature:** Simulation (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 12 · **Used by:** 3

## Types
- `SimulationFinancialsState` — record, data type (at most one helper method) · 7 properties

## Candidates
- **move-to-feature** — Used only by the Simulation feature; move it into that feature's folder.

## Uses
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
