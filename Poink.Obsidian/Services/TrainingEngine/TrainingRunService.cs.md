---
tags: [service]
source: Poink.Sim/Services/TrainingEngine/TrainingRunService.cs
---
# TrainingRunService.cs

Owns TrainPageV2's training run -- its network, metrics and Start/Step/Pause/Stop/Reset -- so a run carries on in the background when the page is left. The page attaches a TrainingRunObserver to drive the Live tab.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
- [[Services/TrainingEngine/ActionAssembler.cs.md|ActionAssembler.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/TrainingEngineService.cs.md|TrainingEngineService.cs]]
- [[Services/TrainingEngine/TrainingRunModels.cs.md|TrainingRunModels.cs]]
