---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 22
types: 1
methods: 2
fan_in: 4
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/ActionAssembler.cs
---
# ActionAssembler.cs

Turns OutputOptionsModel into an ActionSpace -- which directions are enabled, in what order, and how outputs decode.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 22 · **Used by:** 4

## Types
- `ActionAssembler` — class, class with behaviour · 2 methods

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/TrainingModels/OutputOptionsModel.cs.md|OutputOptionsModel.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
