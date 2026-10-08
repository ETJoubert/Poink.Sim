---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/PolicyGradientLearner.cs
---
# PolicyGradientLearner.cs

Turns one episode's recorded decisions into a network weight update: discount rewards into returns, normalize by the episode's own mean/std as a variance-reducing baseline, then backprop each step's advantage-weighted softmax gradient plus an entropy bonus (ported from PolicyGradientTrainingStrategy: coefficient 0.01,…

**Used by:** 3 files (see Backlinks)

## Uses
- [[Models/TrainingModels/TrainingStrategyModels.cs.md|TrainingStrategyModels.cs]]
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/AdamOptimizer.cs.md|AdamOptimizer.cs]]
- [[Services/TrainingEngine/DecisionTrace.cs.md|DecisionTrace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/RewardBaseline.cs.md|RewardBaseline.cs]]
