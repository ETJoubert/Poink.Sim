---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 276
types: 1
methods: 10
fan_in: 0
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/TradeConsole/TradeConsole.razor.cs
---
# TradeConsole.razor.cs

Order-entry panel shared by the live Trade page and the Simulation page -- order type, direction, volume, SL/TP and entry/projection tables.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 276 · **Used by:** 0

## Types
- `TradeConsole` — class, Razor component · 10 methods, 56 properties, 16 fields

## Uses
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Components/Shared/TradeConsole/TradeConsoleModels.cs.md|TradeConsoleModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
