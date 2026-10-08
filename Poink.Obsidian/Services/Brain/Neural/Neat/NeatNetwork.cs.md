---
tags: [class, feature/brain, kind/static-helper, candidate/helper-sprawl]
role: class
feature: Brain
feature_source: folder
kind: static-helper
code_lines: 58
types: 1
methods: 2
fan_in: 1
fan_out: 1
summary_source: generated
candidates: [helper-sprawl]
source: Poink.Sim/Services/Brain/Neural/Neat/NeatNetwork.cs
---
# NeatNetwork.cs

Defines neat network.

**Feature:** Brain · **Kind:** static helper · **Code lines:** 58 · **Used by:** 1

## Types
- `NeatNetwork` — class, static helper · 2 methods

## Candidates
- **helper-sprawl** — One of 3 small static helpers in `Services/Brain/Neural/Neat`; consolidate by purpose with `NeatCrossoverService.cs`, `NeatSpeciationService.cs`.

## Uses
- [[Services/Brain/Neural/Neat/NeatModels.cs.md|NeatModels.cs]]
