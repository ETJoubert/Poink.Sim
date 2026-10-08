---
tags: [class, feature/trainingengine, kind/single-method, candidate/single-method]
role: class
feature: TrainingEngine
feature_source: folder
kind: single-method
code_lines: 22
types: 1
methods: 1
fan_in: 1
fan_out: 2
summary_source: documented
candidates: [single-method]
source: Poink.Sim/Services/TrainingEngine/DecisionMask.cs
---
# DecisionMask.cs

Gates whether the network is even asked to decide -- once per closed bar, once per configured interval, or on every tick. Action-legality masking lives in the Output tab, not here.

**Feature:** TrainingEngine · **Kind:** single-method class · **Code lines:** 22 · **Used by:** 1

## Types
- `DecisionMask` — class, single-method class · 1 method, 3 fields, 1 constructor

## Candidates
- **single-method** — Inline `ShouldDecide` into its only user `TrainingEngineService.cs`.

## Uses
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
