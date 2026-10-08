---
tags: [helper, feature/shared, kind/static-helper]
role: helper
feature: Shared
feature_source: shared
kind: static-helper
code_lines: 31
types: 1
methods: 4
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Helpers/SplitTickReadHelper.cs
---
# SplitTickReadHelper.cs

Reads one tick range that may span two tables: [from, historyBeforeUtc) from a history table and the rest from the main table. The Trade page needs this on a demo account, whose history lives in the live table while today's ticks are recorded in the demo one.

**Feature:** Shared · **Kind:** static helper · **Code lines:** 31 · **Used by:** 3

## Types
- `SplitTickReadHelper` — class, static helper · 4 methods, 1 field

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
