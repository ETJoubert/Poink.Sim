---
tags: [class, feature/brain, kind/behavior]
role: class
feature: Brain
feature_source: folder
kind: behavior
code_lines: 20
types: 1
methods: 2
fan_in: 4
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Brain/BrainRegistry.cs
---
# BrainRegistry.cs

Holds every DI-registered ISimulationBrain instance so callers can list or look one up by name without depending on the DI container directly.

**Feature:** Brain · **Kind:** class with behaviour · **Code lines:** 20 · **Used by:** 4

## Types
- `BrainRegistry` — class, class with behaviour · 2 methods, 1 property, 2 fields, 1 constructor

## Uses
- [[Services/Brain/ISimulationBrain.cs.md|ISimulationBrain.cs]]
