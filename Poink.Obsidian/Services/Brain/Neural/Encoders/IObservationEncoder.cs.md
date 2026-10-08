---
tags: [interface, feature/brain, kind/mixed]
role: interface
feature: Brain
feature_source: folder
kind: mixed
code_lines: 10
types: 2
methods: 1
fan_in: 12
fan_out: 2
summary_source: generated
candidates: []
source: Poink.Sim/Services/Brain/Neural/Encoders/IObservationEncoder.cs
---
# IObservationEncoder.cs

Contract for the observation encoder.

**Feature:** Brain · **Kind:** mixed types · **Code lines:** 10 · **Used by:** 12

## Types
- `FeatureDescriptor` — record struct, data type (at most one helper method) · 2 properties
- `IObservationEncoder` — interface, interface · 1 method, 4 properties

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/Neural/Normalizers/IFeatureNormalizer.cs.md|IFeatureNormalizer.cs]]
