---
tags: [component, feature/trade, kind/component]
role: component
feature: Trade
feature_source: folder
kind: component
code_lines: 60
types: 1
methods: 0
fan_in: 3
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/QuickTradeConsole/QuickTradeConsole.razor
---
# QuickTradeConsole.razor

One-click market trading for the charted symbol: a volume and a LONG and a SHORT button. A side's button turns into CLOSE while that side has open positions on the symbol. The page owns the positions and executes requests.

**Feature:** Trade · **Kind:** Razor component · **Code lines:** 60 · **Used by:** 3

## Types
- `QuickTradeConsole` — component, Razor component · empty

## Uses
- [[Components/Pages/Trade/Components/ColumnZoneStack/ColumnZoneStack.razor.md|ColumnZoneStack.razor]]
- [[Components/Pages/Trade/Components/QuickTradeAutomation/QuickTradeAutomation.razor.md|QuickTradeAutomation.razor]]
- [[Components/Pages/Trade/Helpers/QuickTradeHelper.cs.md|QuickTradeHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
