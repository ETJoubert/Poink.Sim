---
tags: [class, feature/trader, kind/single-method]
role: class
feature: Trader
feature_source: folder
kind: single-method
code_lines: 13
types: 1
methods: 1
fan_in: 1
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/Trader/TraderFactory.cs
---
# TraderFactory.cs

Builds a Trader for one session and wraps it with risk management according to the session's settings.

**Feature:** Trader · **Kind:** single-method class · **Code lines:** 13 · **Used by:** 1

## Types
- `TraderFactory` — class, single-method class · 1 method, 1 field, 1 constructor

## Uses
- [[Services/Trader/ITrader.cs.md|ITrader.cs]]
- [[Services/Trader/ITraderFactory.cs.md|ITraderFactory.cs]]
- [[Services/Trader/Risk/IRiskPolicyService.cs.md|IRiskPolicyService.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
