---
tags: [class, feature/emulator, kind/behavior]
role: class
feature: Emulator
feature_source: folder
kind: behavior
code_lines: 88
types: 1
methods: 13
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/Matching/OrderMatchingEngine.cs
---
# OrderMatchingEngine.cs

Price-time priority limit order book on a whole-tick grid. An incoming order fills against the best opposite levels at their resting prices, oldest first, and any remainder rests. An incoming order never fills against its own bot: the bot's resting order is cancelled instead, so the book can never cross.

**Feature:** Emulator · **Kind:** class with behaviour · **Code lines:** 88 · **Used by:** 1

## Types
- `OrderMatchingEngine` — class, class with behaviour · 13 methods, 4 properties, 4 fields

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/Matching/MatchingModels.cs.md|MatchingModels.cs]]
