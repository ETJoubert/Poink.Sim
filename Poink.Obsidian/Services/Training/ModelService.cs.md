---
tags: [service, feature/training, kind/mixed]
role: service
feature: Training
feature_source: folder
kind: mixed
code_lines: 131
types: 2
methods: 7
fan_in: 7
fan_out: 6
summary_source: generated
candidates: []
source: Poink.Sim/Services/Training/ModelService.cs
---
# ModelService.cs

Service that handles model.

**Feature:** Training · **Kind:** mixed types · **Code lines:** 131 · **Used by:** 7

## Types
- `NeuralWeightsSummary` — record, data type (at most one helper method) · 12 properties
- `ModelService` — class, class with behaviour · 7 methods, 2 properties, 3 fields, 1 constructor, 1 event

## Uses
- [[Models/NeuralWeightsModels.cs.md|NeuralWeightsModels.cs]]
- [[Repository/ModelTrainingParameters.cs.md|ModelTrainingParameters.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Training/Interfaces/IModelService.cs.md|IModelService.cs]]
- [[Services/Training/Interfaces/INeuralNetworkService.cs.md|INeuralNetworkService.cs]]
- [[Services/Training/Interfaces/INeuralWeightsService.cs.md|INeuralWeightsService.cs]]
