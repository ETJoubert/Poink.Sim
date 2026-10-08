---
tags: [class, feature/simulation, kind/behavior]
role: class
feature: Simulation
feature_source: folder
kind: behavior
code_lines: 57
types: 1
methods: 7
fan_in: 5
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/Simulation/SimulationMarketFeed.cs
---
# SimulationMarketFeed.cs

The market a run's signals and bots read, built tick by tick: the most recent completed bars with their price histograms, the bar still forming, and a running typical spread. Shared by the interactive simulation and the headless signal-dataset builder, so both see exactly the same inputs.

**Feature:** Simulation · **Kind:** class with behaviour · **Code lines:** 57 · **Used by:** 5

## Types
- `SimulationMarketFeed` — class, class with behaviour · 7 methods, 5 properties, 5 fields

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
- [[Services/Simulation/RingBuffer.cs.md|RingBuffer.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
