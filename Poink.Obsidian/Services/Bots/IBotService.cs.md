---
tags: [interface, feature/bots, kind/interface]
role: interface
feature: Bots
feature_source: folder
kind: interface
code_lines: 9
types: 1
methods: 3
fan_in: 3
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/Bots/IBotService.cs
---
# IBotService.cs

Owns the bots and their trading accounts and decides, on each simulation tick, when they trade. The simulation feeds it market data; it applies the resulting orders to the bots' accounts.

**Feature:** Bots · **Kind:** interface · **Code lines:** 9 · **Used by:** 3

## Types
- `IBotService` — interface, interface · 3 methods, 1 property

## Uses
- [[Models/BotAccountOptions.cs.md|BotAccountOptions.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
- [[Services/Trader/Risk/RiskPolicyToggles.cs.md|RiskPolicyToggles.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
