---
tags: [service]
source: Poink.Sim/Services/MarketData/Import/ZTicksImportService.cs
---
# ZTicksImportService.cs

Writes through the same ITickIngest the live backfill worker uses, so imported history is indistinguishable from streamed ticks.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/Import/IZTicksImportService.cs.md|IZTicksImportService.cs]]
- [[Services/MarketData/Import/ZTicksImportModels.cs.md|ZTicksImportModels.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
