---
tags: [class, feature/emulator, kind/single-method]
role: class
feature: Emulator
feature_source: folder
kind: single-method
code_lines: 28
types: 1
methods: 1
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/Bots/RandomBotStrategy.cs
---
# RandomBotStrategy.cs

Zero-intelligence trader: sometimes cancels one of its resting orders at random; otherwise rolls its group's chances to buy, sell or close its position (doing nothing for the rest). A buy or sell goes at a random level within OrderRangeTicks of the last price for a random volume its margin allows.

**Feature:** Emulator · **Kind:** single-method class · **Code lines:** 28 · **Used by:** 1

## Types
- `RandomBotStrategy` — class, single-method class · 1 method, 1 field, 2 constructors

## Uses
- [[Services/Emulator/Bots/EmulatorBotModels.cs.md|EmulatorBotModels.cs]]
- [[Services/Emulator/Bots/IEmulatorBotStrategy.cs.md|IEmulatorBotStrategy.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
