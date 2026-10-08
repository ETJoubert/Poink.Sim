---
tags: [service, feature/training, kind/mixed]
role: service
feature: Training
feature_source: folder
kind: mixed
code_lines: 317
types: 3
methods: 13
fan_in: 5
fan_out: 11
summary_source: generated
candidates: []
source: Poink.Sim/Services/Training/BrainService.cs
---
# BrainService.cs

Service that handles brain.

**Feature:** Training · **Kind:** mixed types · **Code lines:** 317 · **Used by:** 5

## Types
- `BrainOption` — record, data type (at most one helper method) · 2 properties
- `NeuralNetworkSummary` — record, data type (at most one helper method) · 5 properties
- `BrainService` — class, class with behaviour · 13 methods, 5 properties, 5 fields, 1 constructor, 2 events

## Uses
- [[Models/NeuralNetworkModels.cs.md|NeuralNetworkModels.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/BrainRegistry.cs.md|BrainRegistry.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/ComposedPolicyBrain.cs.md|ComposedPolicyBrain.cs]]
- [[Services/Brain/Neural/INeatPolicyBrain.cs.md|INeatPolicyBrain.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Brain/Neural/Neat/NeatModels.cs.md|NeatModels.cs]]
- [[Services/Brain/Neural/NeatPolicyBrain.cs.md|NeatPolicyBrain.cs]]
- [[Services/Training/Interfaces/IBrainService.cs.md|IBrainService.cs]]
- [[Services/Training/Interfaces/INeuralNetworkService.cs.md|INeuralNetworkService.cs]]
