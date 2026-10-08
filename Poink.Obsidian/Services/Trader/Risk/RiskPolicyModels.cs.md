---
tags: [model, feature/trader, kind/mixed]
role: model
feature: Trader
feature_source: folder
kind: mixed
code_lines: 46
types: 8
methods: 3
fan_in: 20
fan_out: 4
summary_source: generated
candidates: []
source: Poink.Sim/Services/Trader/Risk/RiskPolicyModels.cs
---
# RiskPolicyModels.cs

Models, records and DTOs for risk policy.

**Feature:** Trader · **Kind:** mixed types · **Code lines:** 46 · **Used by:** 20

## Types
- `RiskContext` — record struct, data type (at most one helper method) · 8 properties
- `RiskPolicyInfo` — record struct, data type (at most one helper method) · 2 properties
- `RiskPolicyParameter` — record struct, data type (at most one helper method) · 6 properties
- `RiskPolicySnapshot` — record, data type (at most one helper method) · 4 properties
- `RiskPolicyStatus` — record struct, data type (at most one helper method) · 2 properties
- `RiskPolicyDenial` — record struct, data type (at most one helper method) · 3 properties
- `RiskVerdict` — enum, enum · 3 values
- `RiskReview` — record struct, class with behaviour · 3 methods, 4 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
