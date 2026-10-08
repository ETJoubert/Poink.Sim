---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 42
types: 1
methods: 3
fan_in: 9
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/MlpNetwork.cs
---
# MlpNetwork.cs

A fully-connected feed-forward network: Dense(input -> hidden[0]) -ReLU-> ... -> Dense(hidden[^1] -> output), output layer linear (ActionSpace decodes it downstream).

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 42 · **Used by:** 9

## Types
- `MlpNetwork` — class, class with behaviour · 3 methods, 5 properties, 1 constructor

## Uses
- _nothing in Poink.Sim_
