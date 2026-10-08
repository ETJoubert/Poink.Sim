---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/NetworkAssembler.cs
---
# NetworkAssembler.cs

Turns NetworkOptionsModel plus the sizes Feature/Action assemblers produced into a trainable network. Only MLP is implemented -- LSTM/CNN throw rather than silently building the wrong architecture.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Models/TrainingModels/NetworkOptionsModel.cs.md|NetworkOptionsModel.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
