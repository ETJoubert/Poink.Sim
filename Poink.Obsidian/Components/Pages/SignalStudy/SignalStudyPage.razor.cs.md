---
tags: [code-behind, feature/signalstudy, kind/component-code]
role: code-behind
feature: SignalStudy
feature_source: folder
kind: component-code
code_lines: 186
types: 1
methods: 19
fan_in: 0
fan_out: 12
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/SignalStudy/SignalStudyPage.razor.cs
---
# SignalStudyPage.razor.cs

Builds signal datasets -- the rule engine's signals stored once per second for one fixed profile of settings -- and lists the ones stored so far. The analysis of what follows each signal comes later.

**Feature:** SignalStudy · **Kind:** component code-behind · **Code lines:** 186 · **Used by:** 0

## Types
- `SignalStudyPage` — class, Razor component · 19 methods, 11 properties, 17 fields

## Uses
- [[Components/Pages/SignalStudy/SignalStudyPage.razor.md|SignalStudyPage.razor]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
- [[Models/SimulationPageSettings.cs.md|SimulationPageSettings.cs]]
- [[Services/SignalStudy/ISignalAnalysisService.cs.md|ISignalAnalysisService.cs]]
- [[Services/SignalStudy/ISignalDatasetBuilder.cs.md|ISignalDatasetBuilder.cs]]
- [[Services/SignalStudy/ISignalDatasetService.cs.md|ISignalDatasetService.cs]]
- [[Services/SignalStudy/SignalAnalysisCsv.cs.md|SignalAnalysisCsv.cs]]
- [[Services/Symbols/ISymbolTableResolver.cs.md|ISymbolTableResolver.cs]]
- [[Services/Symbols/SymbolTableResolver.cs.md|SymbolTableResolver.cs]]
- [[wwwroot/js/fileDownload.js.md|fileDownload.js]]
