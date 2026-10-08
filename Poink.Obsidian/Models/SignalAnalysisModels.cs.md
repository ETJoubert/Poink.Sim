---
tags: [model, feature/signalstudy, kind/data-bundle, candidate/move-to-feature]
role: model
feature: SignalStudy
feature_source: usage
kind: data-bundle
code_lines: 45
types: 4
methods: 0
fan_in: 9
fan_out: 2
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/SignalAnalysisModels.cs
---
# SignalAnalysisModels.cs

How a signal dataset is labelled and ranked: the forward-return horizons, the stop/target/time-limit trade tested from each entry, the fewest entries a state needs to be ranked, and how many top states are combined into pairs.

**Feature:** SignalStudy (by usage) · **Kind:** bundle of data types · **Code lines:** 45 · **Used by:** 9

## Types
- `SignalAnalysisOptions` — record, data type (at most one helper method) · 7 properties
- `SignalTradeSide` — enum, enum · 2 values
- `SignalStateStats` — record, data type (at most one helper method) · 18 properties
- `SignalAnalysisResult` — record, data type (at most one helper method) · 8 properties

## Candidates
- **move-to-feature** — Used only by the SignalStudy feature; move it into that feature's folder.

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
