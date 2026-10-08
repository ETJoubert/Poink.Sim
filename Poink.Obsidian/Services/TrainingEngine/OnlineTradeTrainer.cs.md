---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/OnlineTradeTrainer.cs
---
# OnlineTradeTrainer.cs

Credits Trade Outcome learning with trade outcomes for one episode. An entry waits until its trade closes, whatever closed it, and is then rewarded with that trade's realized value. A close is rewarded with the trade it closed, and any other choice with zero.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/DecisionTrace.cs.md|DecisionTrace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/PolicyGradientLearner.cs.md|PolicyGradientLearner.cs]]
