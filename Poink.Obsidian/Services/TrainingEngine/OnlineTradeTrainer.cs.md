---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 31
types: 1
methods: 2
fan_in: 1
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/OnlineTradeTrainer.cs
---
# OnlineTradeTrainer.cs

Credits Trade Outcome learning with trade outcomes for one episode. An entry waits until its trade closes, whatever closed it, and is then rewarded with that trade's realized value. A close is rewarded with the trade it closed, and any other choice with zero.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 31 · **Used by:** 1

## Types
- `OnlineTradeTrainer` — class, class with behaviour · 2 methods, 6 fields, 1 constructor

## Uses
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/DecisionTrace.cs.md|DecisionTrace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
- [[Services/TrainingEngine/PolicyGradientLearner.cs.md|PolicyGradientLearner.cs]]
