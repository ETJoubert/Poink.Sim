---
tags: [helper, feature/shared, kind/mixed]
role: helper
feature: Shared
feature_source: shared
kind: mixed
code_lines: 228
types: 6
methods: 15
fan_in: 15
fan_out: 8
summary_source: generated
candidates: []
source: Poink.Sim/Helpers/RuleEngineHelper.cs
---
# RuleEngineHelper.cs

Helper methods for rule engine.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 228 · **Used by:** 15

## Types
- `RuleSignalGroup` — enum, enum · 8 values
- `RuleSignalRow` — record struct, data type (at most one helper method) · 10 properties
- `RuleEngineResult` — record struct, data type (at most one helper method) · 8 properties
- `RuleEngineHelper` — class, static helper · 12 methods
- `Keys` — class, nested, static constants · 23 fields
- `TxFoldKeys` — class, nested, static helper · 3 methods

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldStats.cs.md|TxFoldStats.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Helpers/RuleSignalRowsHelper.cs.md|RuleSignalRowsHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
