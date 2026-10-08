---
tags: [service, feature/emulator, kind/behavior]
role: service
feature: Emulator
feature_source: folder
kind: behavior
code_lines: 263
types: 1
methods: 23
fan_in: 1
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/EmulatorService.cs
---
# EmulatorService.cs

Process-wide emulated brokerage for one synthetic asset. Each step, bots in random order each get a chance to act on what they alone can see; the broker fills their orders, which moves the price, then stops out under-margined bots.

**Feature:** Emulator · **Kind:** class with behaviour · **Code lines:** 263 · **Used by:** 1

## Types
- `EmulatorService` — class, class with behaviour · 23 methods, 4 properties, 14 fields, 2 constructors, 1 event

## Uses
- [[Services/Emulator/Bots/IEmulatorBotStrategy.cs.md|IEmulatorBotStrategy.cs]]
- [[Services/Emulator/Broker/EmulatorBroker.cs.md|EmulatorBroker.cs]]
- [[Services/Emulator/Broker/EmulatorBrokerModels.cs.md|EmulatorBrokerModels.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/IEmulatorService.cs.md|IEmulatorService.cs]]
