---
tags: [service, feature/training, kind/single-method, candidate/single-method-service]
role: service
feature: Training
feature_source: folder
kind: single-method
code_lines: 25
types: 1
methods: 1
fan_in: 3
fan_out: 4
summary_source: generated
candidates: [single-method-service]
source: Poink.Sim/Services/Training/RewardPolicies/RewardPolicyService.cs
---
# RewardPolicyService.cs

Service that handles reward policy.

**Feature:** Training · **Kind:** single-method class · **Code lines:** 25 · **Used by:** 3

## Types
- `RewardPolicyService` — class, single-method class · 1 method, 3 properties, 3 fields, 1 constructor

## Candidates
- **single-method-service** — Only `SelectPolicy` behind `IRewardPolicyService`, its sole implementation; fold into a broader service unless it is a deliberate seam.

## Uses
- [[Services/Training/RewardPolicies/EquityDriftRewardPolicy.cs.md|EquityDriftRewardPolicy.cs]]
- [[Services/Training/RewardPolicies/HitRateRewardPolicy.cs.md|HitRateRewardPolicy.cs]]
- [[Services/Training/RewardPolicies/IRewardPolicy.cs.md|IRewardPolicy.cs]]
- [[Services/Training/RewardPolicies/IRewardPolicyService.cs.md|IRewardPolicyService.cs]]
