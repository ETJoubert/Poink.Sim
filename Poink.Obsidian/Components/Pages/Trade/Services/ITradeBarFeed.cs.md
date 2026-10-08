---
tags: [interface]
source: Poink.Sim/Components/Pages/Trade/Services/ITradeBarFeed.cs
---
# ITradeBarFeed.cs

Live bars for every symbol the Trade page charts or compares: each is loaded once through the database-first tick history, then kept current from the session's tick stream.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Components/Pages/Trade/Services/TradeSymbolSeries.cs.md|TradeSymbolSeries.cs]]
