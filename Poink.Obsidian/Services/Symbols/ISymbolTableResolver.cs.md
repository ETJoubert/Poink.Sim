---
tags: [interface, feature/symbols, kind/mixed]
role: interface
feature: Symbols
feature_source: folder
kind: mixed
code_lines: 8
types: 2
methods: 1
fan_in: 6
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Services/Symbols/ISymbolTableResolver.cs
---
# ISymbolTableResolver.cs

Resolves a tick table to the symbol it holds and that symbol's trading metadata.

**Feature:** Symbols · **Kind:** mixed types · **Code lines:** 8 · **Used by:** 6

## Types
- `SymbolTableResolution` — record, data type (at most one helper method) · 3 properties
- `ISymbolTableResolver` — interface, interface · 1 method

## Uses
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Helpers/SymbolTradingLimits.cs.md|SymbolTradingLimits.cs]]
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
