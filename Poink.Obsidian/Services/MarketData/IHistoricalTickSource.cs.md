---
tags: [interface]
source: Poink.Sim/Services/MarketData/IHistoricalTickSource.cs
---
# IHistoricalTickSource.cs

Fetches historical ticks for the pre-stream gap the backfill worker cannot fill from the live stream. Backed by cTrader's ProtoOAGetTickData (bid + ask series merged into mid quotes) once implemented; the current stub returns nothing, so only empty or already-current tables persist.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
