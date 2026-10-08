---
tags: [class]
source: Poink.Sim/Components/Pages/Trade/Services/TradeLiveTickSource.cs
---
# TradeLiveTickSource.cs

Feeds the Trade compare TxFold tick by tick: each symbol's held series supplies every tick from its first one on (loaded history plus live ticks not yet written), and only the older part of a window is read from the tick tables -- once, then cached, since the window's start only moves forward as the anchor follows the…

**Used by:** 2 files (see Backlinks)

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Components/Charts/CompareTxFold/ICompareTxFoldTickSource.cs.md|ICompareTxFoldTickSource.cs]]
- [[Components/Pages/Trade/Services/ITradeBarFeed.cs.md|ITradeBarFeed.cs]]
- [[Helpers/SplitTickReadHelper.cs.md|SplitTickReadHelper.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
