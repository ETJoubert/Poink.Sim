---
tags: [class]
source: Poink.Sim/Services/MarketData/TickTableNaming.cs
---
# TickTableNaming.cs

Maps a cTrader symbol name to its Postgres tick table. Convention: "live_ticks_" or "demo_ticks_" (depending on the account environment) + the symbol name lower-cased with every non-alphanumeric character removed (e.g. "EURUSD" on a live account -> "live_ticks_eurusd").

**Used by:** 9 files (see Backlinks)

## Uses
- _nothing in Poink.Sim_
