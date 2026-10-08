---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 50
types: 1
methods: 2
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/MlpWeightsSerializer.cs
---
# MlpWeightsSerializer.cs

Converts an MlpNetwork to and from bytes for storage: a format version, the layer sizes, then each layer's weights (row-major) followed by its biases, all little-endian.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 50 · **Used by:** 2

## Types
- `MlpWeightsSerializer` — class, class with behaviour · 2 methods, 1 field

## Uses
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
