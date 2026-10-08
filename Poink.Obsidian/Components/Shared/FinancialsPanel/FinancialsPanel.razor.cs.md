---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 60
types: 2
methods: 8
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/FinancialsPanel/FinancialsPanel.razor.cs
---
# FinancialsPanel.razor.cs

Tabbed Positions/History/Statistics shell plus a Balance/Equity/Margin/Realized/Unrealized footer, shared by the live Trade page and the Simulation page.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 60 · **Used by:** 1

## Types
- `FinancialsPanel` — class, Razor component · 8 methods, 17 properties, 2 fields
- `FinancialsTab` — enum, nested, enum · 4 values

## Uses
- [[Components/Shared/FinancialsPanel/FinancialsPanel.razor.md|FinancialsPanel.razor]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
