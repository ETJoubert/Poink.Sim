---
tags: [service, feature/trader, kind/behavior]
role: service
feature: Trader
feature_source: folder
kind: behavior
code_lines: 37
types: 1
methods: 3
fan_in: 5
fan_out: 13
summary_source: documented
candidates: []
source: Poink.Sim/Services/Trader/Risk/RiskPolicyService.cs
---
# RiskPolicyService.cs

Catalog of every IRiskPolicy in this assembly, and the seam that wraps a core trader in the risk decorator for a run's selected policies.

**Feature:** Trader · **Kind:** class with behaviour · **Code lines:** 37 · **Used by:** 5

## Types
- `RiskPolicyService` — class, class with behaviour · 3 methods, 1 property, 1 field, 1 constructor

## Uses
- [[Services/Trader/ITrader.cs.md|ITrader.cs]]
- [[Services/Trader/Risk/IRiskPolicy.cs.md|IRiskPolicy.cs]]
- [[Services/Trader/Risk/IRiskPolicyService.cs.md|IRiskPolicyService.cs]]
- [[Services/Trader/Risk/Policies/LossStreakVolumePolicy.cs.md|LossStreakVolumePolicy.cs]]
- [[Services/Trader/Risk/Policies/MaxOpenRiskPolicy.cs.md|MaxOpenRiskPolicy.cs]]
- [[Services/Trader/Risk/Policies/PercentStopLossPolicy.cs.md|PercentStopLossPolicy.cs]]
- [[Services/Trader/Risk/Policies/ProfitGivebackPolicy.cs.md|ProfitGivebackPolicy.cs]]
- [[Services/Trader/Risk/PositionManager.cs.md|PositionManager.cs]]
- [[Services/Trader/Risk/RiskAwareTrader.cs.md|RiskAwareTrader.cs]]
- [[Services/Trader/Risk/RiskManager.cs.md|RiskManager.cs]]
- [[Services/Trader/Risk/RiskPolicyModels.cs.md|RiskPolicyModels.cs]]
- [[Services/Trader/Risk/RiskPolicyToggles.cs.md|RiskPolicyToggles.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
