---
tags: [class, feature/brain, kind/behavior]
role: class
feature: Brain
feature_source: folder
kind: behavior
code_lines: 144
types: 2
methods: 6
fan_in: 2
fan_out: 9
summary_source: generated
candidates: []
source: Poink.Sim/Services/Brain/Neural/Encoders/TraderStateEncoder.cs
---
# TraderStateEncoder.cs

Defines trader state encoder.

**Feature:** Brain · **Kind:** class with behaviour · **Code lines:** 144 · **Used by:** 2

## Types
- `TraderStateEncoder` — class, class with behaviour · 6 methods, 4 properties, 9 fields, 1 constructor
- `BaseFieldDefinition` — record, nested, data type (at most one helper method) · 4 properties

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/Encoders/IObservationEncoder.cs.md|IObservationEncoder.cs]]
- [[Services/Brain/Neural/Normalizers/BinaryNormalizer.cs.md|BinaryNormalizer.cs]]
- [[Services/Brain/Neural/Normalizers/DirectionNormalizer.cs.md|DirectionNormalizer.cs]]
- [[Services/Brain/Neural/Normalizers/IFeatureNormalizer.cs.md|IFeatureNormalizer.cs]]
- [[Services/Brain/Neural/Normalizers/RawPriceNormalizer.cs.md|RawPriceNormalizer.cs]]
- [[Services/Brain/Neural/Normalizers/RawValueNormalizer.cs.md|RawValueNormalizer.cs]]
