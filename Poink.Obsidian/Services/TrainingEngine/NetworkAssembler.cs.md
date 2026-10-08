---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 33
types: 1
methods: 4
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/NetworkAssembler.cs
---
# NetworkAssembler.cs

Turns NetworkOptionsModel plus the sizes Feature/Action assemblers produced into a trainable network. Only MLP is implemented -- LSTM/CNN throw rather than silently building the wrong architecture.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 33 · **Used by:** 3

## Types
- `NetworkAssembler` — class, class with behaviour · 4 methods

## Uses
- [[Models/TrainingModels/NetworkOptionsModel.cs.md|NetworkOptionsModel.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
