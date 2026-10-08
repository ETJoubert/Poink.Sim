---
tags: [settings, feature/shared, kind/data, candidate/merge-data]
role: settings
feature: Shared
feature_source: shared
kind: data
code_lines: 23
types: 1
methods: 0
fan_in: 7
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/RuleSignalSettings.cs
---
# RuleSignalSettings.cs

Thresholds and lookbacks for the event, zone, regime, session and position rule-engine signals, shared by every rule set and the Rule Engine tab. Defaults are starting points to tune, not proven values.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 23 · **Used by:** 7

## Types
- `RuleSignalSettings` — record, data type (at most one helper method) · 21 properties

## Candidates
- **merge-data** — Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `StatGroup.cs`.

## Uses
- _nothing in Poink.Sim_
