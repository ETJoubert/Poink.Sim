---
tags: [repository, feature/trainingengine, kind/behavior]
role: repository
feature: TrainingEngine
feature_source: usage
kind: behavior
code_lines: 110
types: 1
methods: 4
fan_in: 2
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Repository/TrainingHistoryRepository.cs
---
# TrainingHistoryRepository.cs

Stores the saved training results that belong to each trained model.

**Feature:** TrainingEngine (by usage) · **Kind:** class with behaviour · **Code lines:** 110 · **Used by:** 2

## Types
- `TrainingHistoryRepository` — class, class with behaviour · 4 methods, 2 fields, 1 constructor

## Uses
- [[Data/Entities/TrainingHistoryEntry.cs.md|TrainingHistoryEntry.cs]]
- [[Data/PoinkSimContext.cs.md|PoinkSimContext.cs]]
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
