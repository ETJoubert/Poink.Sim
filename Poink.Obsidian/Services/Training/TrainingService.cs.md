---
tags: [service, feature/training, kind/mixed]
role: service
feature: Training
feature_source: folder
kind: mixed
code_lines: 687
types: 2
methods: 11
fan_in: 2
fan_out: 31
summary_source: generated
candidates: []
source: Poink.Sim/Services/Training/TrainingService.cs
---
# TrainingService.cs

Service that handles training.

**Feature:** Training · **Kind:** mixed types · **Code lines:** 687 · **Used by:** 2

## Types
- `RlTrainingLogEntry` — record, data type (at most one helper method) · 3 properties
- `TrainingService` — class, class with behaviour · 11 methods, 30 properties, 28 fields, 1 constructor, 1 event

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/NeuralNetworkModels.cs.md|NeuralNetworkModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Models/TrainingSaveModels.cs.md|TrainingSaveModels.cs]]
- [[Models/TrainingStartModels.cs.md|TrainingStartModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/ModelTrainingParameters.cs.md|ModelTrainingParameters.cs]]
- [[Services/Brain/BrainRegistry.cs.md|BrainRegistry.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/INeatPolicyBrain.cs.md|INeatPolicyBrain.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Brain/Neural/Neat/NeatModels.cs.md|NeatModels.cs]]
- [[Services/Brain/Neural/Neat/NeatTopologyDescriber.cs.md|NeatTopologyDescriber.cs]]
- [[Services/Simulation/DummyTickGenerator.cs.md|DummyTickGenerator.cs]]
- [[Services/Simulation/RingBuffer.cs.md|RingBuffer.cs]]
- [[Services/Trader/Commission/PercentOfNotionalCommission.cs.md|PercentOfNotionalCommission.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Training/EpisodeTimeConstraint.cs.md|EpisodeTimeConstraint.cs]]
- [[Services/Training/EpisodeWindowSampler.cs.md|EpisodeWindowSampler.cs]]
- [[Services/Training/Interfaces/IBrainService.cs.md|IBrainService.cs]]
- [[Services/Training/Interfaces/IModelService.cs.md|IModelService.cs]]
- [[Services/Training/Interfaces/INeuralNetworkService.cs.md|INeuralNetworkService.cs]]
- [[Services/Training/Interfaces/IRlTrainingStrategy.cs.md|IRlTrainingStrategy.cs]]
- [[Services/Training/Interfaces/ITrainingService.cs.md|ITrainingService.cs]]
- [[Services/Training/ModelService.cs.md|ModelService.cs]]
- [[Services/Training/RewardPolicies/IRewardPolicyService.cs.md|IRewardPolicyService.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
- [[Services/Training/RlEpisodeRunner.cs.md|RlEpisodeRunner.cs]]
