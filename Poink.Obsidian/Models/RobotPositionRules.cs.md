---
tags: [model, feature/shared, kind/data-bundle, candidate/merge-data]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 24
types: 3
methods: 0
fan_in: 8
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/RobotPositionRules.cs
---
# RobotPositionRules.cs

Rule-based placement and management of the robot's positions, layered over the Positioning console: stops and targets from volatility or market structure, breakeven and trailing stops, and smaller size in a drawdown. With the modes on Console and every switch off, the robot positions exactly as the console says.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 24 · **Used by:** 8

## Types
- `RobotStopMode` — enum, enum · 3 values
- `RobotTargetMode` — enum, enum · 3 values
- `RobotPositionRules` — record, data type (at most one helper method) · 14 properties

## Candidates
- **merge-data** — Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`.

## Uses
- _nothing in Poink.Sim_
