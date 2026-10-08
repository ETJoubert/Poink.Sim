---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 48
types: 1
methods: 15
fan_in: 6
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/ICTraderSessionService.cs
---
# ICTraderSessionService.cs

Single-user cTrader Open API session for this dashboard: owns the OAuth token and the one live connection, exposing connection state, the authorized account's symbols, and a live-streaming watchlist to the UI. The token is held in memory only, so a restart requires logging in again (no refresh-token persistence yet).

**Feature:** CTrader · **Kind:** interface · **Code lines:** 48 · **Used by:** 6

## Types
- `ICTraderSessionService` — interface, interface · 15 methods, 12 properties, 2 events

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
