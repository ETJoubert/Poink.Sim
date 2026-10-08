---
tags: [class]
source: Poink.Sim/Components/Pages/Trade/Services/TradeSymbolSeries.cs
---
# TradeSymbolSeries.cs

One symbol's bars at one bar size: aggregated from its loaded history, then advanced by live ticks. Each update swaps in new lists rather than mutating, so a render holding the old ones never sees a half-applied tick and reference comparisons tell when bars changed.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
