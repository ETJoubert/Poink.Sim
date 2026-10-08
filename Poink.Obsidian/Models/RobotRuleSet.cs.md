---
tags: [model, feature/shared, kind/data, candidate/merge-data]
role: model
feature: Shared
feature_source: shared
kind: data
code_lines: 16
types: 1
methods: 1
fan_in: 5
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/RobotRuleSet.cs
---
# RobotRuleSet.cs

One rule-engine configuration the robot evaluates -- the Above/In/Below keys chosen in the rule panel plus the XGrid Top % cutoff those rows use. Mutable and parameterless so the panel can edit it in place and the Simulation page can persist it with its other settings.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 16 · **Used by:** 5

## Types
- `RobotRuleSet` — class, data type (at most one helper method) · 1 method, 5 properties

## Candidates
- **merge-data** — Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`.

## Uses
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
