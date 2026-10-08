---
tags: [code-behind, feature/trade, kind/component-code]
role: code-behind
feature: Trade
feature_source: folder
kind: component-code
code_lines: 76
types: 1
methods: 6
fan_in: 0
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/QuickTradeAutomation/QuickTradeAutomation.razor.cs
---
# QuickTradeAutomation.razor.cs

The quick console's automation toggles: auto-arm after t, a market time stop after t + s, and auto-fire on a zone gate pass. t, s and the two close toggles are kept per browser; auto-fire always starts off, like ARMED.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 76 · **Used by:** 0

## Types
- `QuickTradeAutomation` — class, Razor component · 6 methods, 8 properties, 5 fields

## Uses
- [[Components/Pages/Trade/Components/QuickTradeAutomation/QuickTradeAutomation.razor.md|QuickTradeAutomation.razor]]
- [[Components/Pages/Trade/Helpers/PositionCloseHelper.cs.md|PositionCloseHelper.cs]]
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
