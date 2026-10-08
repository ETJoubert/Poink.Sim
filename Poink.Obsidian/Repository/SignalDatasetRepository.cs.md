---
tags: [repository, feature/signalstudy, kind/behavior]
role: repository
feature: SignalStudy
feature_source: usage
kind: behavior
code_lines: 180
types: 4
methods: 9
fan_in: 1
fan_out: 2
summary_source: interface-doc
candidates: []
source: Poink.Sim/Repository/SignalDatasetRepository.cs
---
# SignalDatasetRepository.cs

Stores signal datasets: one header per dataset and its per-second signal points.

**Feature:** SignalStudy (by usage) · **Kind:** class with behaviour · **Code lines:** 180 · **Used by:** 1

## Types
- `SignalDatasetRepository` — class, class with behaviour · 9 methods, 7 fields, 1 constructor
- `DatasetRow` — record, nested, data type (at most one helper method) · 12 properties
- `PointRow` — record, nested, data type (at most one helper method) · 7 properties
- `Sql` — class, nested, static constants · 11 fields

## Uses
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
- [[Repository/Interfaces/ISignalDatasetRepository.cs.md|ISignalDatasetRepository.cs]]
