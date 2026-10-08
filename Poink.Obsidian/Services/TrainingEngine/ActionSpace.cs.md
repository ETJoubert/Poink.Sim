---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 44
types: 1
methods: 5
fan_in: 6
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/ActionSpace.cs
---
# ActionSpace.cs

The network's output shape and decoding rule, built from OutputOptionsModel. Position size, stop and target come from Position Control, so the network only outputs a direction.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 44 · **Used by:** 6

## Types
- `ActionSpace` — record, class with behaviour · 5 methods, 5 properties

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
