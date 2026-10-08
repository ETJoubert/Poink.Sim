---
tags: [code-behind, feature/trade, kind/component-code]
role: code-behind
feature: Trade
feature_source: folder
kind: component-code
code_lines: 183
types: 2
methods: 21
fan_in: 0
fan_out: 12
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/QuickTradeConsole/QuickTradeConsole.razor.cs
---
# QuickTradeConsole.razor.cs

One-click market trading for the charted symbol: a volume and a LONG and a SHORT button. A side's button turns into CLOSE while that side has open positions on the symbol. The page owns the positions and executes requests.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 183 · **Used by:** 0

## Types
- `QuickTradeConsole` — class, Razor component · 21 methods, 26 properties, 10 fields
- `CountdownBar` — record, nested, data type (at most one helper method) · 4 properties

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Components/Pages/Trade/Components/QuickTradeConsole/QuickTradeConsole.razor.md|QuickTradeConsole.razor]]
- [[Components/Pages/Trade/Helpers/ColumnZoneHelper.cs.md|ColumnZoneHelper.cs]]
- [[Components/Pages/Trade/Helpers/PositionCloseHelper.cs.md|PositionCloseHelper.cs]]
- [[Components/Pages/Trade/Helpers/QuickTradeHelper.cs.md|QuickTradeHelper.cs]]
- [[Components/Pages/Trade/Models/ColumnZoneModels.cs.md|ColumnZoneModels.cs]]
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Components/Pages/Trade/Models/QuickTradeModels.cs.md|QuickTradeModels.cs]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
