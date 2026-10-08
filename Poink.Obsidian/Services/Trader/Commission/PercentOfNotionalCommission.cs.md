---
tags: [class, feature/trader, kind/single-method, candidate/single-method-service]
role: class
feature: Trader
feature_source: folder
kind: single-method
code_lines: 5
types: 1
methods: 1
fan_in: 5
fan_out: 2
summary_source: generated
candidates: [single-method-service]
source: Poink.Sim/Services/Trader/Commission/PercentOfNotionalCommission.cs
---
# PercentOfNotionalCommission.cs

Defines percent of notional commission.

**Feature:** Trader · **Kind:** single-method class · **Code lines:** 5 · **Used by:** 5

## Types
- `PercentOfNotionalCommission` — class, single-method class · 1 method, 1 property, 1 field

## Candidates
- **single-method-service** — Only `CommissionFor` behind `ICommissionModel`, its sole implementation; fold into a broader service unless it is a deliberate seam.

## Uses
- [[Services/Trader/Commission/ICommissionModel.cs.md|ICommissionModel.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
