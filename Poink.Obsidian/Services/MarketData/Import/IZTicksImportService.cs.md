---
tags: [interface, feature/marketdata, kind/interface]
role: interface
feature: MarketData
feature_source: folder
kind: interface
code_lines: 14
types: 1
methods: 3
fan_in: 4
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/Import/IZTicksImportService.cs
---
# IZTicksImportService.cs

Reads cTrader's local .zticks backtesting cache and writes it through ITickIngest.

**Feature:** MarketData · **Kind:** interface · **Code lines:** 14 · **Used by:** 4

## Types
- `IZTicksImportService` — interface, interface · 3 methods, 5 properties, 1 event

## Uses
- [[Services/MarketData/Import/ZTicksImportModels.cs.md|ZTicksImportModels.cs]]
