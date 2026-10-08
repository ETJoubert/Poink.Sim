---
tags: [class, feature/simulation, kind/behavior]
role: class
feature: Simulation
feature_source: folder
kind: behavior
code_lines: 52
types: 1
methods: 4
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Simulation/PrefetchingTickReader.cs
---
# PrefetchingTickReader.cs

Reads ticks from a source enumerator on a background task in fixed-size blocks, so fetching the next ticks overlaps with replaying the current ones. Yields exactly the source's ticks, in order.

**Feature:** Simulation · **Kind:** class with behaviour · **Code lines:** 52 · **Used by:** 2

## Types
- `PrefetchingTickReader` — class, class with behaviour · 4 methods, 2 properties, 6 fields, 1 constructor

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
