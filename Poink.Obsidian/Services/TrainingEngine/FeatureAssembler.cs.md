---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 118
types: 1
methods: 12
fan_in: 3
fan_out: 8
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/FeatureAssembler.cs
---
# FeatureAssembler.cs

Turns FeatureOptionsModel's selections into a FeatureModel (structured, for display/inspection) and a flattened FeatureVector (for training), from live tick data and a trader-state snapshot.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 118 · **Used by:** 3

## Types
- `FeatureAssembler` — class, class with behaviour · 12 methods, 1 field

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Models/TrainingModels/FeatureModel.cs.md|FeatureModel.cs]]
- [[Models/TrainingModels/FeatureOptionsModel.cs.md|FeatureOptionsModel.cs]]
- [[Models/TrainingModels/FeatureVector.cs.md|FeatureVector.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/TrainingEngine/TickWindow.cs.md|TickWindow.cs]]
- [[Services/TrainingEngine/TraderStateSnapshot.cs.md|TraderStateSnapshot.cs]]
