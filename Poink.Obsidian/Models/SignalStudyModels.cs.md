---
tags: [model, feature/signalstudy, kind/data-bundle, candidate/move-to-feature]
role: model
feature: SignalStudy
feature_source: usage
kind: data-bundle
code_lines: 63
types: 7
methods: 0
fan_in: 12
fan_out: 2
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/SignalStudyModels.cs
---
# SignalStudyModels.cs

The fixed set of scalar settings a signal dataset is built with -- bar size, the rule engine's window and grid, the In band and every signal threshold. A dataset holds the signals for exactly one profile; another profile is another dataset.

**Feature:** SignalStudy (by usage) · **Kind:** bundle of data types · **Code lines:** 63 · **Used by:** 12

## Types
- `SignalProfile` — record, data type (at most one helper method) · 8 properties
- `SignalCatalogEntry` — record, data type (at most one helper method) · 7 properties
- `SignalDatasetStatus` — enum, enum · 4 values
- `SignalDatasetInfo` — record, data type (at most one helper method) · 12 properties
- `SignalPoint` — record struct, data type (at most one helper method) · 7 properties
- `SignalDatasetBuildRequest` — record, data type (at most one helper method) · 6 properties
- `SignalDatasetBuildProgress` — record, data type (at most one helper method) · 7 properties

## Candidates
- **move-to-feature** — Used only by the SignalStudy feature; move it into that feature's folder.

## Uses
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Models/RuleSignalSettings.cs.md|RuleSignalSettings.cs]]
