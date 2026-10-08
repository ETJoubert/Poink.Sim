---
tags: [settings, feature/shared, kind/data, candidate/merge-data]
role: settings
feature: Shared
feature_source: shared
kind: data
code_lines: 11
types: 1
methods: 0
fan_in: 5
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/BotAccountOptions.cs
---
# BotAccountOptions.cs

How the bots' trading accounts are set up -- applied to every bot when the user sets it.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 11 · **Used by:** 5

## Types
- `BotAccountOptions` — record, data type (at most one helper method) · 7 properties

## Candidates
- **merge-data** — Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`.

## Uses
- [[Helpers/SymbolTradingLimits.cs.md|SymbolTradingLimits.cs]]
