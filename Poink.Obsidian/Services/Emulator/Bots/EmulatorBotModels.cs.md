---
tags: [model, feature/emulator, kind/data-bundle]
role: model
feature: Emulator
feature_source: folder
kind: data-bundle
code_lines: 23
types: 6
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/Bots/EmulatorBotModels.cs
---
# EmulatorBotModels.cs

All one bot may see when it decides: public market data, its own account and its own group's buy/sell/close chances. It carries nothing about other bots.

**Feature:** Emulator · **Kind:** bundle of data types · **Code lines:** 23 · **Used by:** 3

## Types
- `BotMarketView` — record, data type (at most one helper method) · 13 properties
- `BotDecision` — record, data type (at most one helper method) · 1 property
- `HoldDecision` — record, data type (at most one helper method) · empty
- `PlaceOrderDecision` — record, data type (at most one helper method) · 3 properties
- `CancelOrderDecision` — record, data type (at most one helper method) · 1 property
- `ClosePositionDecision` — record, data type (at most one helper method) · empty

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
