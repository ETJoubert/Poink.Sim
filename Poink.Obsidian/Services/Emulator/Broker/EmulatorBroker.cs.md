---
tags: [class, feature/emulator, kind/behavior]
role: class
feature: Emulator
feature_source: folder
kind: behavior
code_lines: 244
types: 1
methods: 33
fan_in: 1
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/Broker/EmulatorBroker.cs
---
# EmulatorBroker.cs

The brokerage: holds every bot's account, checks each order against the bot's margin and the broker fund, routes it to the order book, settles fills into the accounts and stops out bots whose margin level falls too low. The last fill sets the price.

**Feature:** Emulator · **Kind:** class with behaviour · **Code lines:** 244 · **Used by:** 1

## Types
- `EmulatorBroker` — class, class with behaviour · 33 methods, 2 properties, 18 fields, 1 constructor

## Uses
- [[Helpers/EmulatorTickHelper.cs.md|EmulatorTickHelper.cs]]
- [[Services/Emulator/Bots/EmulatorBotModels.cs.md|EmulatorBotModels.cs]]
- [[Services/Emulator/Broker/EmulatorBrokerModels.cs.md|EmulatorBrokerModels.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/Matching/MatchingModels.cs.md|MatchingModels.cs]]
- [[Services/Emulator/Matching/OrderMatchingEngine.cs.md|OrderMatchingEngine.cs]]
