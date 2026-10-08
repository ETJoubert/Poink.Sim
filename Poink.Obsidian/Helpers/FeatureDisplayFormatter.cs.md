---
tags: [class, feature/training, kind/mixed, candidate/move-to-feature]
role: class
feature: Training
feature_source: usage
kind: mixed
code_lines: 67
types: 4
methods: 6
fan_in: 3
fan_out: 4
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Helpers/FeatureDisplayFormatter.cs
---
# FeatureDisplayFormatter.cs

Shared by TrainPageV2's Playback (placeholder) and Live (real) tabs so both Features panels stay in sync.

**Feature:** Training (by usage) · **Kind:** mixed types · **Code lines:** 67 · **Used by:** 3

## Types
- `FeatureDisplayRow` — record, data type (at most one helper method) · 3 properties
- `TxFoldGridCell` — record, data type (at most one helper method) · 2 properties
- `TxFoldGridRow` — record, data type (at most one helper method) · 2 properties
- `FeatureDisplayFormatter` — class, static helper · 6 methods

## Candidates
- **move-to-feature** — Used only by the Training feature; move it into that feature's folder.

## Uses
- [[Components/Charts/TxFold/Helper/SnapshotTimeframeOptions.cs.md|SnapshotTimeframeOptions.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/TrainingModels/FeatureModel.cs.md|FeatureModel.cs]]
