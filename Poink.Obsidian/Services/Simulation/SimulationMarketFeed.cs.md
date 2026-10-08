---
tags: [class]
source: Poink.Sim/Services/Simulation/SimulationMarketFeed.cs
---
# SimulationMarketFeed.cs

The market a run's signals and bots read, built tick by tick: the most recent completed bars with their price histograms, the bar still forming, and a running typical spread. Shared by the interactive simulation and the headless signal-dataset builder, so both see exactly the same inputs.

**Used by:** 5 files (see Backlinks)

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
- [[Services/Simulation/RingBuffer.cs.md|RingBuffer.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
