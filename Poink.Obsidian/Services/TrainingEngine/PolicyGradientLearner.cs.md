---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 155
types: 1
methods: 16
fan_in: 3
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/PolicyGradientLearner.cs
---
# PolicyGradientLearner.cs

Turns one episode's recorded decisions into a network weight update: discount rewards into returns, normalize by the episode's own mean/std as a variance-reducing baseline, then backprop each step's advantage-weighted softmax gradient plus an entropy bonus (ported from PolicyGradientTrainingStrategy: coefficient 0.01,…

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 155 · **Used by:** 3

## Types
- `PolicyGradientLearner` — class, class with behaviour · 16 methods, 6 fields

## Uses
- [[Models/TrainingModels/TrainingStrategyModels.cs.md|TrainingStrategyModels.cs]]
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/AdamOptimizer.cs.md|AdamOptimizer.cs]]
- [[Services/TrainingEngine/DecisionTrace.cs.md|DecisionTrace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/RewardBaseline.cs.md|RewardBaseline.cs]]
