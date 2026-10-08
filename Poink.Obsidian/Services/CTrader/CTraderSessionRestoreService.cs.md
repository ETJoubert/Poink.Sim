---
tags: [service, feature/ctrader, kind/behavior]
role: service
feature: CTrader
feature_source: folder
kind: behavior
code_lines: 27
types: 1
methods: 2
fan_in: 1
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/CTraderSessionRestoreService.cs
---
# CTraderSessionRestoreService.cs

Restores any persisted cTrader session in the background at startup, so a prior login reconnects without blocking application boot or requiring a fresh OAuth redirect.

**Feature:** CTrader · **Kind:** class with behaviour · **Code lines:** 27 · **Used by:** 1

## Types
- `CTraderSessionRestoreService` — class, class with behaviour · 2 methods, 2 fields, 1 constructor

## Uses
- [[Services/CTrader/ICTraderSessionService.cs.md|ICTraderSessionService.cs]]
