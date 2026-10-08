---
tags: [model, feature/shared, kind/data-bundle, candidate/merge-data]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 8
types: 3
methods: 0
fan_in: 5
fan_out: 0
summary_source: generated
candidates: [merge-data]
source: Poink.Sim/Models/StatGroup.cs
---
# StatGroup.cs

Models, records and DTOs for stat group.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 8 · **Used by:** 5

## Types
- `StatEmphasis` — enum, enum · 3 values
- `StatItem` — record, data type (at most one helper method) · 3 properties
- `StatGroup` — record, data type (at most one helper method) · 2 properties

## Candidates
- **merge-data** — Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`.

## Uses
- _nothing in Poink.Sim_
