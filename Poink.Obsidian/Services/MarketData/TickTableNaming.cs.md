---
tags: [class, feature/marketdata, kind/single-method, candidate/single-method]
role: class
feature: MarketData
feature_source: folder
kind: single-method
code_lines: 8
types: 1
methods: 1
fan_in: 9
fan_out: 0
summary_source: documented
candidates: [single-method]
source: Poink.Sim/Services/MarketData/TickTableNaming.cs
---
# TickTableNaming.cs

Maps a cTrader symbol name to its Postgres tick table. Convention: "live_ticks_" or "demo_ticks_" (depending on the account environment) + the symbol name lower-cased with every non-alphanumeric character removed (e.g. "EURUSD" on a live account -> "live_ticks_eurusd").

**Feature:** MarketData · **Kind:** single-method class · **Code lines:** 8 · **Used by:** 9

## Types
- `TickTableNaming` — class, single-method class · 1 method, 2 fields

## Candidates
- **single-method** — `ForSymbol` is used by 9 files; check it earns its own type.

## Uses
- _nothing in Poink.Sim_
