---
tags: [page, feature/signalstudy, kind/page]
role: page
feature: SignalStudy
feature_source: folder
kind: page
code_lines: 210
types: 1
methods: 4
fan_in: 2
fan_out: 8
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/SignalStudy/SignalStudyPage.razor
---
# SignalStudyPage.razor

Builds signal datasets -- the rule engine's signals stored once per second for one fixed profile of settings -- and lists the ones stored so far. The analysis of what follows each signal comes later.

**Feature:** SignalStudy · **Kind:** Razor page · **Code lines:** 210 · **Used by:** 2

## Types
- `SignalStudyPage` — component, Razor component · 4 methods

## Uses
- [[Components/Pages/SignalStudy/Components/SignalStatsTable/SignalStatsTable.razor.md|SignalStatsTable.razor]]
- [[Components/RuleEngine/RuleSignalSettingsPanel.razor.md|RuleSignalSettingsPanel.razor]]
- [[Components/Shared/BarSizeSelect/BarSizeSelect.razor.md|BarSizeSelect.razor]]
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
- [[Components/Shared/TickDataSource/TickDataSource.razor.md|TickDataSource.razor]]
- [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]]
- [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]]
- [[Services/Simulation/SimulationMarketFeed.cs.md|SimulationMarketFeed.cs]]
