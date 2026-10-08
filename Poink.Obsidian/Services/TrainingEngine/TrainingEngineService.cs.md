---
tags: [service]
source: Poink.Sim/Services/TrainingEngine/TrainingEngineService.cs
---
# TrainingEngineService.cs

Runs training: builds one network up front, then runs EpisodeCount episodes against it, handing each episode's recorded decisions to PolicyGradientLearner afterward.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Models/TrainingModels/OutputOptionsModel.cs.md|OutputOptionsModel.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Trader/Commission/PercentOfNotionalCommission.cs.md|PercentOfNotionalCommission.cs]]
- [[Services/Trader/ITrader.cs.md|ITrader.cs]]
- [[Services/Trader/ITraderFactory.cs.md|ITraderFactory.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
- [[Services/TrainingEngine/ActionAssembler.cs.md|ActionAssembler.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/DataProvider.cs.md|DataProvider.cs]]
- [[Services/TrainingEngine/DecisionMask.cs.md|DecisionMask.cs]]
- [[Services/TrainingEngine/DecisionTrace.cs.md|DecisionTrace.cs]]
- [[Services/TrainingEngine/EpisodeResult.cs.md|EpisodeResult.cs]]
- [[Services/TrainingEngine/FeatureAssembler.cs.md|FeatureAssembler.cs]]
- [[Services/TrainingEngine/LiveBarBuilder.cs.md|LiveBarBuilder.cs]]
- [[Services/TrainingEngine/LiveEpisodeSnapshot.cs.md|LiveEpisodeSnapshot.cs]]
- [[Services/TrainingEngine/LiveTickSnapshot.cs.md|LiveTickSnapshot.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/NetworkAssembler.cs.md|NetworkAssembler.cs]]
- [[Services/TrainingEngine/OnlineTradeTrainer.cs.md|OnlineTradeTrainer.cs]]
- [[Services/TrainingEngine/PolicyGradientLearner.cs.md|PolicyGradientLearner.cs]]
- [[Services/TrainingEngine/PositionControlHelper.cs.md|PositionControlHelper.cs]]
- [[Services/TrainingEngine/TickWindow.cs.md|TickWindow.cs]]
- [[Services/TrainingEngine/TraderStateSnapshot.cs.md|TraderStateSnapshot.cs]]
