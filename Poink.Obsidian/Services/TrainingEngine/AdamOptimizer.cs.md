---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 41
types: 1
methods: 4
fan_in: 1
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/AdamOptimizer.cs
---
# AdamOptimizer.cs

Adam weight updates for one network: per-weight running averages of the gradient and its square scale each step, so the learning rate means the same thing whatever the gradient's size. State is per instance, so each network being trained needs its own.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 41 · **Used by:** 1

## Types
- `AdamOptimizer` — class, class with behaviour · 4 methods, 8 fields, 1 constructor

## Uses
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
