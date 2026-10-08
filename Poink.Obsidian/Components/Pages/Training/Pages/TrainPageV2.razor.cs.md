---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 912
types: 8
methods: 82
fan_in: 1
fan_out: 45
summary_source: generated
candidates: []
source: Poink.Sim/Components/Pages/Training/Pages/TrainPageV2.razor.cs
---
# TrainPageV2.razor.cs

Code-behind logic for the TrainPageV2 component.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 912 · **Used by:** 1

## Types
- `TrainPageV2` — class, Razor component · 82 methods, 81 properties, 106 fields
- `TrainV2Tab` — enum, nested, enum · 7 values
- `NetworkColumn` — record, nested, data type (at most one helper method) · 4 properties
- `MaskRuleRow` — record, nested, data type (at most one helper method) · 4 properties
- `EpisodeSamplingModeOption` — record, nested, data type (at most one helper method) · 3 properties
- `DecisionMaskResolutionOption` — record, nested, data type (at most one helper method) · 3 properties
- `LiveUiState` — record, nested, data type (at most one helper method) · 5 properties
- `LivePlaybackPreferences` — record, nested, data type (at most one helper method) · 2 properties

## Uses
- [[Components/Charts/EquityCurve/EquityCurveHelper.cs.md|EquityCurveHelper.cs]]
- [[Components/Charts/EquityCurve/EquityCurveSample.cs.md|EquityCurveSample.cs]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Trade/Helpers/PositionPnlHelper.cs.md|PositionPnlHelper.cs]]
- [[Components/Pages/Training/Components/TrainedModelsPanel/TrainedModelsPanel.razor.md|TrainedModelsPanel.razor]]
- [[Components/Pages/Training/Components/TrainedModelsPanel/TrainedModelsPanel.razor.cs.md|TrainedModelsPanel.razor.cs]]
- [[Components/Pages/Training/Pages/TrainPageV2.razor.md|TrainPageV2.razor]]
- [[Helpers/FeatureDisplayFormatter.cs.md|FeatureDisplayFormatter.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/ProfilerModels.cs.md|ProfilerModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Models/TrainingModels/DataOptionsModel.cs.md|DataOptionsModel.cs]]
- [[Models/TrainingModels/FeatureOptionsModel.cs.md|FeatureOptionsModel.cs]]
- [[Models/TrainingModels/NetworkOptionsModel.cs.md|NetworkOptionsModel.cs]]
- [[Models/TrainingModels/OutputOptionsModel.cs.md|OutputOptionsModel.cs]]
- [[Models/TrainingModels/PositionControlModels.cs.md|PositionControlModels.cs]]
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Models/TrainingModels/TrainingStrategyModels.cs.md|TrainingStrategyModels.cs]]
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Profiler/IProfilerService.cs.md|IProfilerService.cs]]
- [[Services/Profiler/ProfilerService.cs.md|ProfilerService.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
- [[Services/Symbols/ISymbolService.cs.md|ISymbolService.cs]]
- [[Services/Symbols/SymbolService.cs.md|SymbolService.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Training/EpisodeTimeConstraint.cs.md|EpisodeTimeConstraint.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
- [[Services/TrainingEngine/LiveEpisodeSnapshot.cs.md|LiveEpisodeSnapshot.cs]]
- [[Services/TrainingEngine/LiveTickSnapshot.cs.md|LiveTickSnapshot.cs]]
- [[Services/TrainingEngine/TrainedModelService.cs.md|TrainedModelService.cs]]
- [[Services/TrainingEngine/TrainingHistoryService.cs.md|TrainingHistoryService.cs]]
- [[Services/TrainingEngine/TrainingRunModels.cs.md|TrainingRunModels.cs]]
- [[Services/TrainingEngine/TrainingRunService.cs.md|TrainingRunService.cs]]
