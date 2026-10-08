---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 151
types: 1
methods: 5
fan_in: 0
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/LiveEpisodeView/LiveEpisodeView.razor.cs
---
# LiveEpisodeView.razor.cs

Renders whichever panels are toggled on for the episode TrainPageV2's Run tab currently has in progress -- all data comes in as parameters, so it stays a pure display component.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 151 · **Used by:** 0

## Types
- `LiveEpisodeView` — class, Razor component · 5 methods, 60 properties, 2 fields

## Uses
- [[Components/Charts/EquityCurve/EquityCurveSample.cs.md|EquityCurveSample.cs]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Pages/Training/Components/LiveEpisodeView/LiveEpisodeView.razor.md|LiveEpisodeView.razor]]
- [[Helpers/FeatureDisplayFormatter.cs.md|FeatureDisplayFormatter.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
