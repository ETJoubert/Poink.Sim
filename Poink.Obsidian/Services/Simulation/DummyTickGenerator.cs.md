---
tags: [class, feature/simulation, kind/single-method, candidate/single-method]
role: class
feature: Simulation
feature_source: folder
kind: single-method
code_lines: 20
types: 1
methods: 1
fan_in: 1
fan_out: 1
summary_source: generated
candidates: [single-method]
source: Poink.Sim/Services/Simulation/DummyTickGenerator.cs
---
# DummyTickGenerator.cs

Defines dummy tick generator.

**Feature:** Simulation · **Kind:** single-method class · **Code lines:** 20 · **Used by:** 1

## Types
- `DummyTickGenerator` — class, single-method class · 1 method

## Candidates
- **single-method** — Inline `Generate` into its only user `TrainingService.cs`.

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
