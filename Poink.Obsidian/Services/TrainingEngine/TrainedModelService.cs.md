---
tags: [service, feature/trainingengine, kind/behavior]
role: service
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 79
types: 1
methods: 6
fan_in: 2
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/TrainedModelService.cs
---
# TrainedModelService.cs

Saves, lists, loads and deletes trained models. A model is stored as the options it was trained under plus its serialized weights; loading rebuilds the network and checks its shape still fits those options, since weights only work with the input and output sizes they were trained with.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 79 · **Used by:** 2

## Types
- `TrainedModelService` — class, class with behaviour · 6 methods, 4 fields, 1 constructor

## Uses
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Repository/TrainedModelRepository.cs.md|TrainedModelRepository.cs]]
- [[Services/TrainingEngine/ActionAssembler.cs.md|ActionAssembler.cs]]
- [[Services/TrainingEngine/FeatureAssembler.cs.md|FeatureAssembler.cs]]
- [[Services/TrainingEngine/LoadedTrainedModel.cs.md|LoadedTrainedModel.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/MlpWeightsSerializer.cs.md|MlpWeightsSerializer.cs]]
- [[Services/TrainingEngine/NetworkAssembler.cs.md|NetworkAssembler.cs]]
