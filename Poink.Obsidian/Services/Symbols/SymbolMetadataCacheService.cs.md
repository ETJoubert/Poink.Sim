---
tags: [service, feature/symbols, kind/behavior]
role: service
feature: Symbols
feature_source: folder
kind: behavior
code_lines: 33
types: 1
methods: 3
fan_in: 1
fan_out: 4
summary_source: interface-doc
candidates: []
source: Poink.Sim/Services/Symbols/SymbolMetadataCacheService.cs
---
# SymbolMetadataCacheService.cs

Per-session cache of symbol metadata (pip size, digits, etc.) used for pip/P&amp;L math across the Trade page's components -- checks the DB first, falling back to cTrader (and persisting the result) only the first time a symbol is needed.

**Feature:** Symbols · **Kind:** class with behaviour · **Code lines:** 33 · **Used by:** 1

## Types
- `SymbolMetadataCacheService` — class, class with behaviour · 3 methods, 1 property, 4 fields, 1 constructor

## Uses
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
- [[Services/Symbols/ISymbolService.cs.md|ISymbolService.cs]]
