---
tags: [component, feature/trade, kind/component]
role: component
feature: Trade
feature_source: folder
kind: component
code_lines: 64
types: 1
methods: 0
fan_in: 3
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/TradeAccountMenu/TradeAccountMenu.razor
---
# TradeAccountMenu.razor

Connection status badge, account switcher and log in/out for the Trade page's header -- split out so TradePage doesn't own account/session chrome directly.

**Feature:** Trade · **Kind:** Razor component · **Code lines:** 64 · **Used by:** 3

## Types
- `TradeAccountMenu` — component, Razor component · empty

## Uses
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/OrderBot/OrderBotModels.cs.md|OrderBotModels.cs]]
