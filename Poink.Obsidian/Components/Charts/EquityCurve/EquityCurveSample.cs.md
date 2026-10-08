---
tags: [class, feature/equitycurve, kind/data]
role: class
feature: EquityCurve
feature_source: folder
kind: data
code_lines: 9
types: 1
methods: 0
fan_in: 6
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/EquityCurve/EquityCurveSample.cs
---
# EquityCurveSample.cs

One trade on an EquityCurve: Equity is the equity at the trade's outcome, TradeMaxEquity/TradeMinEquity the best/worst equity reached while it was open, and TradeNumber its 1-based sequence among all trades -- callers build these with EquityCurveHelper.

**Feature:** EquityCurve · **Kind:** data type (at most one helper method) · **Code lines:** 9 · **Used by:** 6

## Types
- `EquityCurveSample` — record struct, data type (at most one helper method) · 5 properties

## Uses
- _nothing in Poink.Sim_
