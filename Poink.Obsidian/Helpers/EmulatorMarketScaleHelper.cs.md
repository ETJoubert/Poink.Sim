---
tags: [helper]
source: Poink.Sim/Helpers/EmulatorMarketScaleHelper.cs
---
# EmulatorMarketScaleHelper.cs

Fits one price scale for the chart and the ladder to share: the price history (with the last price and the spread) fills the middle half, and the quarters above and below are left for the order book. Book levels beyond those margins fall off the scale rather than squeezing the history.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Helpers/EmulatorChartHelper.cs.md|EmulatorChartHelper.cs]]
- [[Helpers/EmulatorTickHelper.cs.md|EmulatorTickHelper.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
