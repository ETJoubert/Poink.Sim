---
tags: [class, feature/brain, kind/single-method, candidate/single-method]
role: class
feature: Brain
feature_source: folder
kind: single-method
code_lines: 38
types: 1
methods: 1
fan_in: 2
fan_out: 5
summary_source: generated
candidates: [single-method]
source: Poink.Sim/Services/Brain/Neural/DecisionMasking/DecisionMasker.cs
---
# DecisionMasker.cs

Defines decision masker.

**Feature:** Brain · **Kind:** single-method class · **Code lines:** 38 · **Used by:** 2

## Types
- `DecisionMasker` — class, single-method class · 1 method, 1 property, 5 fields, 1 constructor

## Candidates
- **single-method** — `ShouldDecide` is used by 2 files; check it earns its own type.

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/DecisionMasking/IDecisionMaskRule.cs.md|IDecisionMaskRule.cs]]
- [[Services/Simulation/PriceCellTracker.cs.md|PriceCellTracker.cs]]
