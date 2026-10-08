---
tags: [interface, feature/emulator, kind/interface]
role: interface
feature: Emulator
feature_source: folder
kind: interface
code_lines: 18
types: 1
methods: 10
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/IEmulatorService.cs
---
# IEmulatorService.cs

Drives the brokerage emulator: a population of bots trading one synthetic asset through a broker's order book, stepped on a timer while running. The price moves only when the broker fills orders.

**Feature:** Emulator · **Kind:** interface · **Code lines:** 18 · **Used by:** 3

## Types
- `IEmulatorService` — interface, interface · 10 methods, 4 properties, 1 event

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
