---
tags: [class, feature/bots, kind/behavior]
role: class
feature: Bots
feature_source: folder
kind: behavior
code_lines: 39
types: 2
methods: 6
fan_in: 1
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Bots/BotReentryGuard.cs
---
# BotReentryGuard.cs

Holds each bot back from opening again right after a close: for a cooldown of simulated seconds from the close, and, when re-arm is required, until that side's open conditions have stopped matching at least once.

**Feature:** Bots · **Kind:** class with behaviour · **Code lines:** 39 · **Used by:** 1

## Types
- `BotReentryGuard` — class, class with behaviour · 6 methods, 1 field
- `BotState` — class, nested, data type (at most one helper method) · 4 fields

## Uses
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
