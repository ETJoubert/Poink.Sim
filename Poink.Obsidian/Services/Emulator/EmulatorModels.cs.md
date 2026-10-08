---
tags: [model, feature/emulator, kind/mixed]
role: model
feature: Emulator
feature_source: folder
kind: mixed
code_lines: 125
types: 12
methods: 3
fan_in: 26
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/EmulatorModels.cs
---
# EmulatorModels.cs

Which side of the book an order sits on: buying (bid) or selling (ask).

**Feature:** Emulator · **Kind:** mixed types · **Code lines:** 125 · **Used by:** 26

## Types
- `OrderSide` — enum, enum · 2 values
- `OrderBookLevel` — record struct, data type (at most one helper method) · 2 properties
- `OrderBook` — record, data type (at most one helper method) · 6 properties
- `PositionLevel` — record struct, data type (at most one helper method) · 5 properties
- `PopulationSettings` — record, data type (at most one helper method) · 1 method, 13 properties
- `PopulationSummary` — record, data type (at most one helper method) · 13 properties
- `BotSummary` — record, data type (at most one helper method) · 9 properties
- `BrokerSummary` — record, data type (at most one helper method) · 9 properties
- `EmulatorPricePoint` — record struct, data type (at most one helper method) · 5 properties, 1 constructor
- `EmulatorEquityPoint` — record, data type (at most one helper method) · 2 properties
- `EmulatorSnapshot` — record, data type (at most one helper method) · 10 properties
- `EmulatorSettings` — record, class with behaviour · 2 methods, 18 properties, 7 fields

## Uses
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
