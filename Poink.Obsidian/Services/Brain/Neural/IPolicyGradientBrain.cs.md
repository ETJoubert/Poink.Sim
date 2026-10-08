---
tags: [interface, feature/brain, kind/mixed]
role: interface
feature: Brain
feature_source: folder
kind: mixed
code_lines: 71
types: 6
methods: 11
fan_in: 21
fan_out: 3
summary_source: generated
candidates: []
source: Poink.Sim/Services/Brain/Neural/IPolicyGradientBrain.cs
---
# IPolicyGradientBrain.cs

Contract for the policy gradient brain.

**Feature:** Brain · **Kind:** mixed types · **Code lines:** 71 · **Used by:** 21

## Types
- `IPolicyGradientReplay` — interface, interface · empty
- `RlInputFeature` — record, data type (at most one helper method) · 3 properties
- `RlOutputFeature` — record, data type (at most one helper method) · 7 properties
- `NetworkLayerShape` — record, data type (at most one helper method) · 2 properties
- `IDescribableBrain` — interface, interface · 1 method
- `IPolicyGradientBrain` — interface, interface · 10 methods, 2 properties, 1 field

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/ISimulationBrain.cs.md|ISimulationBrain.cs]]
- [[Services/Brain/Neural/Normalizers/IFeatureNormalizer.cs.md|IFeatureNormalizer.cs]]
