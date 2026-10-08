---
tags: [code-behind, feature/trade, kind/component-code]
role: code-behind
feature: Trade
feature_source: folder
kind: component-code
code_lines: 57
types: 1
methods: 7
fan_in: 0
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeAccountMenu/TradeAccountMenu.razor.cs
---
# TradeAccountMenu.razor.cs

Connection status badge, account switcher and log in/out for the Trade page's header -- split out so TradePage doesn't own account/session chrome directly.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 57 · **Used by:** 0

## Types
- `TradeAccountMenu` — class, Razor component · 7 methods, 8 properties, 2 fields

## Uses
- [[Components/Pages/Trade/Components/TradeAccountMenu/TradeAccountMenu.razor.md|TradeAccountMenu.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/OrderBot/IOrderBotStatus.cs.md|IOrderBotStatus.cs]]
- [[Services/OrderBot/OrderBotModels.cs.md|OrderBotModels.cs]]
