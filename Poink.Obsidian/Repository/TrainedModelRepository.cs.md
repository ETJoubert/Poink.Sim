---
tags: [repository, feature/trainingengine, kind/behavior]
role: repository
feature: TrainingEngine
feature_source: usage
kind: behavior
code_lines: 77
types: 1
methods: 4
fan_in: 2
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Repository/TrainedModelRepository.cs
---
# TrainedModelRepository.cs

Stores trained models for the Training Engine. Saving under an existing name overwrites that model in place, keeping its id and history.

**Feature:** TrainingEngine (by usage) · **Kind:** class with behaviour · **Code lines:** 77 · **Used by:** 2

## Types
- `TrainedModelRepository` — class, class with behaviour · 4 methods, 2 fields, 1 constructor

## Uses
- [[Data/Entities/TrainedModel.cs.md|TrainedModel.cs]]
- [[Data/PoinkSimContext.cs.md|PoinkSimContext.cs]]
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
