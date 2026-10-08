---
tags: [model, feature/orderbot, kind/data-bundle]
role: model
feature: OrderBot
feature_source: folder
kind: data-bundle
code_lines: 44
types: 8
methods: 0
fan_in: 8
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/OrderBot/OrderBotModels.cs
---
# OrderBotModels.cs

Wire contract POSTed to the bot's /commands endpoint, mirrored by cBots/OrderBot/OrderBot/OrderBotContract.cs. AccountNumber is the trader login; the bot refuses a command addressed to any account but its own.

**Feature:** OrderBot · **Kind:** bundle of data types · **Code lines:** 44 · **Used by:** 8

## Types
- `OrderBotCommandKind` — enum, enum · 3 values
- `OrderBotOrderType` — enum, enum · 3 values
- `OrderBotCommand` — record, data type (at most one helper method) · 12 properties
- `OrderBotCommandResult` — record, data type (at most one helper method) · 4 properties
- `OrderBotHealth` — record, data type (at most one helper method) · 5 properties
- `OrderBotHealthResult` — record, data type (at most one helper method) · 2 properties
- `OrderBotConnectionState` — enum, enum · 3 values
- `OrderBotStatus` — record, data type (at most one helper method) · 2 properties, 1 field

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
