---
tags: [interface]
source: Poink.Sim/Services/Bots/IBotService.cs
---
# IBotService.cs

Owns the bots and their trading accounts and decides, on each simulation tick, when they trade. The simulation feeds it market data; it applies the resulting orders to the bots' accounts.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Models/BotAccountOptions.cs.md|BotAccountOptions.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
- [[Services/Trader/Risk/RiskPolicyToggles.cs.md|RiskPolicyToggles.cs]]
