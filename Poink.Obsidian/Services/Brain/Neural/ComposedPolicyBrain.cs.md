---
tags: [class, feature/brain, kind/behavior]
role: class
feature: Brain
feature_source: folder
kind: behavior
code_lines: 322
types: 2
methods: 19
fan_in: 4
fan_out: 14
summary_source: generated
candidates: []
source: Poink.Sim/Services/Brain/Neural/ComposedPolicyBrain.cs
---
# ComposedPolicyBrain.cs

Defines composed policy brain.

**Feature:** Brain · **Kind:** class with behaviour · **Code lines:** 322 · **Used by:** 4

## Types
- `ComposedPolicyBrain` — class, class with behaviour · 18 methods, 5 properties, 11 fields, 1 constructor
- `DecisionReplay` — record, nested, single-method class · 1 method, 4 properties

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Brain/Neural/ActionMasking/DirectionActionMasker.cs.md|DirectionActionMasker.cs]]
- [[Services/Brain/Neural/ActionOutputs/IActionOutputHead.cs.md|IActionOutputHead.cs]]
- [[Services/Brain/Neural/Architectures/CnnArchitecture.cs.md|CnnArchitecture.cs]]
- [[Services/Brain/Neural/Architectures/INetworkArchitecture.cs.md|INetworkArchitecture.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/CategoricalSampling.cs.md|CategoricalSampling.cs]]
- [[Services/Brain/Neural/DecisionMasking/DecisionMasker.cs.md|DecisionMasker.cs]]
- [[Services/Brain/Neural/DecisionMasking/IDecisionMaskedBrain.cs.md|IDecisionMaskedBrain.cs]]
- [[Services/Brain/Neural/Encoders/CompositeEncoder.cs.md|CompositeEncoder.cs]]
- [[Services/Brain/Neural/Encoders/ICnnSweepable.cs.md|ICnnSweepable.cs]]
- [[Services/Brain/Neural/Encoders/IObservationEncoder.cs.md|IObservationEncoder.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
