---
tags: [class, feature/brain, kind/single-method, candidate/single-method-service]
role: class
feature: Brain
feature_source: folder
kind: single-method
code_lines: 8
types: 1
methods: 1
fan_in: 1
fan_out: 4
summary_source: generated
candidates: [single-method-service]
source: Poink.Sim/Services/Brain/Neural/DecisionMasking/PriceBelowPocRule.cs
---
# PriceBelowPocRule.cs

Defines price below poc rule.

**Feature:** Brain · **Kind:** single-method class · **Code lines:** 8 · **Used by:** 1

## Types
- `PriceBelowPocRule` — class, single-method class · 1 method, 1 field, 1 constructor

## Candidates
- **single-method-service** — Only `IsSatisfied` behind `IDecisionMaskRule`, its sole implementation; fold into a broader service unless it is a deliberate seam.

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Brain/Neural/DecisionMasking/IDecisionMaskRule.cs.md|IDecisionMaskRule.cs]]
- [[Services/Brain/Neural/Encoders/XGridSignalEncoder.cs.md|XGridSignalEncoder.cs]]
