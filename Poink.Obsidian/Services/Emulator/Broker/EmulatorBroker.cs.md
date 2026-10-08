---
tags: [class]
source: Poink.Sim/Services/Emulator/Broker/EmulatorBroker.cs
---
# EmulatorBroker.cs

The brokerage: holds every bot's account, checks each order against the bot's margin and the broker fund, routes it to the order book, settles fills into the accounts and stops out bots whose margin level falls too low. The last fill sets the price.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Helpers/EmulatorTickHelper.cs.md|EmulatorTickHelper.cs]]
- [[Services/Emulator/Bots/EmulatorBotModels.cs.md|EmulatorBotModels.cs]]
- [[Services/Emulator/Broker/EmulatorBrokerModels.cs.md|EmulatorBrokerModels.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/Matching/MatchingModels.cs.md|MatchingModels.cs]]
- [[Services/Emulator/Matching/OrderMatchingEngine.cs.md|OrderMatchingEngine.cs]]
