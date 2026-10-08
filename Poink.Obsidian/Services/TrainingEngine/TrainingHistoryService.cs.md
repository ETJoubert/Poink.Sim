---
tags: [service, feature/trainingengine, kind/behavior]
role: service
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 27
types: 1
methods: 4
fan_in: 2
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/TrainingHistoryService.cs
---
# TrainingHistoryService.cs

Saves, lists, loads and deletes the training results kept for each trained model, along with the options each run used.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 27 · **Used by:** 2

## Types
- `TrainingHistoryService` — class, class with behaviour · 4 methods, 1 field, 1 constructor

## Uses
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Repository/TrainingHistoryRepository.cs.md|TrainingHistoryRepository.cs]]
