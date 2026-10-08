---
tags: [model, feature/emulator, kind/data-bundle]
role: model
feature: Emulator
feature_source: folder
kind: data-bundle
code_lines: 9
types: 3
methods: 0
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Emulator/Matching/MatchingModels.cs
---
# MatchingModels.cs

An order waiting on the book, with the volume still unfilled.

**Feature:** Emulator · **Kind:** bundle of data types · **Code lines:** 9 · **Used by:** 2

## Types
- `RestingOrder` — class, data type (at most one helper method) · 5 properties
- `OrderSubmission` — record struct, data type (at most one helper method) · 4 properties
- `EmulatorTrade` — record struct, data type (at most one helper method) · 4 properties

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
