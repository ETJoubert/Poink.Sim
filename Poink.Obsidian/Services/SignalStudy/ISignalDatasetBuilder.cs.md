---
tags: [interface, feature/signalstudy, kind/interface]
role: interface
feature: SignalStudy
feature_source: folder
kind: interface
code_lines: 11
types: 1
methods: 2
fan_in: 4
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/SignalStudy/ISignalDatasetBuilder.cs
---
# ISignalDatasetBuilder.cs

Replays a tick range headlessly and stores the rule engine's signals once per second as a signal dataset. One build runs at a time and carries on in the background when the page is left.

**Feature:** SignalStudy · **Kind:** interface · **Code lines:** 11 · **Used by:** 4

## Types
- `ISignalDatasetBuilder` — interface, interface · 2 methods, 4 properties, 1 event

## Uses
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
