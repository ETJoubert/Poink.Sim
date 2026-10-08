---
tags: [model, feature/emulator, kind/mixed]
role: model
feature: Emulator
feature_source: folder
kind: mixed
code_lines: 39
types: 2
methods: 6
fan_in: 2
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/Broker/EmulatorBrokerModels.cs
---
# EmulatorBrokerModels.cs

One bot's account at the broker. Negative cash is points the broker lent for a leveraged buy; a negative position is units it lent for a short sale. Capital is every point paid in (starting capital plus replenishments); equity is cash plus the open position at the given price.

**Feature:** Emulator · **Kind:** mixed types · **Code lines:** 39 · **Used by:** 2

## Types
- `EmulatorBotAccount` — class, class with behaviour · 6 methods, 10 properties
- `OrderOutcome` — enum, enum · 4 values

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
