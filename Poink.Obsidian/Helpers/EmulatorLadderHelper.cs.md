---
tags: [helper, feature/emulator, kind/mixed, candidate/move-to-feature]
role: helper
feature: Emulator
feature_source: usage
kind: mixed
code_lines: 57
types: 2
methods: 4
fan_in: 1
fan_out: 3
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Helpers/EmulatorLadderHelper.cs
---
# EmulatorLadderHelper.cs

Lays an order book out on the shared market scale: one row per scale row, highest price first, each adding up the book levels and positioned bots that fall inside it.

**Feature:** Emulator (by usage) · **Kind:** mixed types · **Code lines:** 57 · **Used by:** 1

## Types
- `EmulatorLadderRow` — record struct, data type (at most one helper method) · 6 properties
- `EmulatorLadderHelper` — class, static helper · 4 methods

## Candidates
- **move-to-feature** — Used only by the Emulator feature; move it into that feature's folder.

## Uses
- [[Helpers/EmulatorMarketScaleHelper.cs.md|EmulatorMarketScaleHelper.cs]]
- [[Helpers/EmulatorTickHelper.cs.md|EmulatorTickHelper.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
