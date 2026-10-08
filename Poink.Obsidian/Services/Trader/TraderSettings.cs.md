---
tags: [settings, feature/trader, kind/data]
role: settings
feature: Trader
feature_source: folder
kind: data
code_lines: 16
types: 1
methods: 0
fan_in: 8
fan_out: 5
summary_source: generated
candidates: []
source: Poink.Sim/Services/Trader/TraderSettings.cs
---
# TraderSettings.cs

Configuration settings for trader.

**Feature:** Trader · **Kind:** data type (at most one helper method) · **Code lines:** 16 · **Used by:** 8

## Types
- `TraderSettings` — record, data type (at most one helper method) · 8 properties

## Uses
- [[Services/Brain/ISimulationBrain.cs.md|ISimulationBrain.cs]]
- [[Services/Trader/Commission/ICommissionModel.cs.md|ICommissionModel.cs]]
- [[Services/Trader/Commission/PercentOfNotionalCommission.cs.md|PercentOfNotionalCommission.cs]]
- [[Services/Trader/Risk/RiskPolicyToggles.cs.md|RiskPolicyToggles.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
