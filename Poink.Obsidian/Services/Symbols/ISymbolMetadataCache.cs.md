---
tags: [interface]
source: Poink.Sim/Services/Symbols/ISymbolMetadataCache.cs
---
# ISymbolMetadataCache.cs

Per-session cache of symbol metadata (pip size, digits, etc.) used for pip/P&amp;L math across the Trade page's components -- checks the DB first, falling back to cTrader (and persisting the result) only the first time a symbol is needed.

**Used by:** 10 files (see Backlinks)

## Uses
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
