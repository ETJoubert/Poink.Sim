---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 706
types: 6
methods: 27
fan_in: 1
fan_out: 23
summary_source: generated
candidates: []
source: Poink.Sim/Components/Pages/Training/Pages/Train/EpisodePlayback.razor.cs
---
# EpisodePlayback.razor.cs

Code-behind logic for the EpisodePlayback component.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 706 · **Used by:** 1

## Types
- `EpisodePlayback` — class, Razor component · 27 methods, 65 properties, 36 fields
- `LiveFeedbackSnapshot` — record, nested, data type (at most one helper method) · 12 properties
- `InputGroup` — record, nested, data type (at most one helper method) · 3 properties
- `StatsTab` — enum, nested, enum · 2 values
- `MetricsRow` — record, nested, data type (at most one helper method) · 3 properties
- `TraderStatsRow` — record, nested, data type (at most one helper method) · 23 properties

## Uses
- [[Components/Charts/EquityCurve/EquityCurveHelper.cs.md|EquityCurveHelper.cs]]
- [[Components/Charts/EquityCurve/EquityCurveSample.cs.md|EquityCurveSample.cs]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Display/XGridLine.cs.md|XGridLine.cs]]
- [[Components/Charts/XGrid/Display/XGridPosition.cs.md|XGridPosition.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Training/Pages/Train/EpisodePlayback.razor.md|EpisodePlayback.razor]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Models/TradeStatistics.cs.md|TradeStatistics.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Training/BrainService.cs.md|BrainService.cs]]
- [[Services/Training/Interfaces/IBrainService.cs.md|IBrainService.cs]]
- [[Services/Training/Interfaces/ITrainingService.cs.md|ITrainingService.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
