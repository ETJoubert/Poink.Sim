---
tags: [service, feature/trainingengine, kind/behavior]
role: service
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 214
types: 1
methods: 16
fan_in: 3
fan_out: 8
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/TrainingRunService.cs
---
# TrainingRunService.cs

Owns TrainPageV2's training run -- its network, metrics and Start/Step/Pause/Stop/Reset -- so a run carries on in the background when the page is left. The page attaches a TrainingRunObserver to drive the Live tab.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 214 · **Used by:** 3

## Types
- `TrainingRunService` — class, class with behaviour · 16 methods, 19 properties, 12 fields, 1 constructor, 1 event

## Uses
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
- [[Services/TrainingEngine/ActionAssembler.cs.md|ActionAssembler.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/TrainingEngineService.cs.md|TrainingEngineService.cs]]
- [[Services/TrainingEngine/TrainingRunModels.cs.md|TrainingRunModels.cs]]
