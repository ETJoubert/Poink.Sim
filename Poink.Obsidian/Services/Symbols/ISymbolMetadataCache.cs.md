---
tags: [interface, feature/symbols, kind/interface]
role: interface
feature: Symbols
feature_source: folder
kind: interface
code_lines: 10
types: 1
methods: 3
fan_in: 9
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Symbols/ISymbolMetadataCache.cs
---
# ISymbolMetadataCache.cs

Per-session cache of symbol metadata (pip size, digits, etc.) used for pip/P&amp;L math across the Trade page's components -- checks the DB first, falling back to cTrader (and persisting the result) only the first time a symbol is needed.

**Feature:** Symbols · **Kind:** interface · **Code lines:** 10 · **Used by:** 9

## Types
- `ISymbolMetadataCache` — interface, interface · 3 methods, 1 property

## Uses
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
