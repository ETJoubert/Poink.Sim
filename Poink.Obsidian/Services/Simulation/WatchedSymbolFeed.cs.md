---
tags: [class, feature/simulation, kind/behavior]
role: class
feature: Simulation
feature_source: folder
kind: behavior
code_lines: 67
types: 1
methods: 6
fan_in: 1
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/Simulation/WatchedSymbolFeed.cs
---
# WatchedSymbolFeed.cs

Replays one watch-only symbol in time lockstep with the traded symbol: each AdvanceTo consumes every tick up to the traded symbol's current timestamp, building that symbol's own bars at the run's aggregation.

**Feature:** Simulation · **Kind:** class with behaviour · **Code lines:** 67 · **Used by:** 1

## Types
- `WatchedSymbolFeed` — class, class with behaviour · 6 methods, 1 property, 11 fields, 1 constructor

## Uses
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SimulationWatchModels.cs.md|SimulationWatchModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Simulation/RingBuffer.cs.md|RingBuffer.cs]]
- [[Services/Simulation/SimulationMarketFeed.cs.md|SimulationMarketFeed.cs]]
