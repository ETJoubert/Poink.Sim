---
tags: [service]
source: Poink.Sim/Services/TrainingEngine/TrainedModelService.cs
---
# TrainedModelService.cs

Saves, lists, loads and deletes trained models. A model is stored as the options it was trained under plus its serialized weights; loading rebuilds the network and checks its shape still fits those options, since weights only work with the input and output sizes they were trained with.

**Used by:** 2 files (see Backlinks)

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
