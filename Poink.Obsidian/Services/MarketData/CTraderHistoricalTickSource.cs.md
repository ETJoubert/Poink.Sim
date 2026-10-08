---
tags: [class, feature/marketdata, kind/single-method]
role: class
feature: MarketData
feature_source: folder
kind: single-method
code_lines: 18
types: 1
methods: 1
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/CTraderHistoricalTickSource.cs
---
# CTraderHistoricalTickSource.cs

Real historical source backed by cTrader ProtoOAGetTickData via the authorized session connection. Available only while the session is connected with a selected account.

**Feature:** MarketData · **Kind:** single-method class · **Code lines:** 18 · **Used by:** 1

## Types
- `CTraderHistoricalTickSource` — class, single-method class · 1 method, 1 property, 1 field, 1 constructor

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/CTrader/ICTraderHistory.cs.md|ICTraderHistory.cs]]
- [[Services/MarketData/IHistoricalTickSource.cs.md|IHistoricalTickSource.cs]]
