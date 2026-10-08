---
tags: [service, feature/brain, kind/single-method, candidate/helper-sprawl, candidate/single-method]
role: service
feature: Brain
feature_source: folder
kind: single-method
code_lines: 41
types: 1
methods: 1
fan_in: 1
fan_out: 1
summary_source: generated
candidates: [helper-sprawl, single-method]
source: Poink.Sim/Services/Brain/Neural/Neat/NeatCrossoverService.cs
---
# NeatCrossoverService.cs

Service that handles neat crossover.

**Feature:** Brain · **Kind:** single-method class · **Code lines:** 41 · **Used by:** 1

## Types
- `NeatCrossoverService` — class, single-method class · 1 method

## Candidates
- **helper-sprawl** — One of 3 small static helpers in `Services/Brain/Neural/Neat`; consolidate by purpose with `NeatNetwork.cs`, `NeatSpeciationService.cs`.
- **single-method** — Inline `Crossover` into its only user `NeatPopulation.cs`.

## Uses
- [[Services/Brain/Neural/Neat/NeatModels.cs.md|NeatModels.cs]]
