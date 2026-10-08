---
tags: [model, feature/training, kind/data-bundle, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data-bundle
code_lines: 27
types: 4
methods: 0
fan_in: 3
fan_out: 3
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/FeatureOptionsModel.cs
---
# FeatureOptionsModel.cs

The Features tab's encoder selections -- TrainPageV2's counterpart to EncoderSizeOptions, scoped to the sources it still exposes (TxFold replaces BarWindow/VolumeProfile/TimeProfile/ SessionClock/SpreadVolatility; XGrid Signals and Trader State carry over unchanged).

**Feature:** Training · **Kind:** bundle of data types · **Code lines:** 27 · **Used by:** 3

## Types
- `XGridSignalsOptionsModel` — record, data type (at most one helper method) · 4 properties
- `TxFoldOptionsModel` — record, data type (at most one helper method) · 8 properties
- `TraderStateOptionsModel` — record, data type (at most one helper method) · 4 properties
- `FeatureOptionsModel` — record, data type (at most one helper method) · 3 properties

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`.

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
