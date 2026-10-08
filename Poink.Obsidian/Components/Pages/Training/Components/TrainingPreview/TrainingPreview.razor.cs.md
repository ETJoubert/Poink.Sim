---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 1107
types: 10
methods: 49
fan_in: 1
fan_out: 27
summary_source: generated
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/TrainingPreview/TrainingPreview.razor.cs
---
# TrainingPreview.razor.cs

Code-behind logic for the TrainingPreview component.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 1107 · **Used by:** 1

## Types
- `TrainingPreview` — class, Razor component · 49 methods, 31 properties, 14 fields
- `RandomEpisodeResult` — record, nested, data type (at most one helper method) · 4 properties
- `EpisodeBarShape` — record, nested, data type (at most one helper method) · 6 properties
- `ProfileBarRow` — record, nested, data type (at most one helper method) · 2 properties
- `ProfileValue` — record, nested, data type (at most one helper method) · 4 properties
- `HiddenLayerBar` — record, nested, data type (at most one helper method) · 3 properties
- `StackSegment` — record, nested, data type (at most one helper method) · 4 properties
- `ActionDecoderOption` — record, data type (at most one helper method) · 6 properties
- `ActionDecoderGroup` — record, data type (at most one helper method) · 5 properties
- `TrainingPreviewSourceRow` — record, data type (at most one helper method) · 21 properties

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Training/Components/TrainingPreview/TrainingPreview.razor.md|TrainingPreview.razor]]
- [[Helpers/TimeProfileHelper.cs.md|TimeProfileHelper.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/VolumeProfileAnalysis.cs.md|VolumeProfileAnalysis.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Services/Brain/Neural/ActionOutputs/DiscretizedLevelsActionHead.cs.md|DiscretizedLevelsActionHead.cs]]
- [[Services/Brain/Neural/BinaryNormalization.cs.md|BinaryNormalization.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/ComposedPolicyBrain.cs.md|ComposedPolicyBrain.cs]]
- [[Services/Brain/Neural/Encoders/IObservationEncoder.cs.md|IObservationEncoder.cs]]
- [[Services/Brain/Neural/Encoders/SessionClockEncoder.cs.md|SessionClockEncoder.cs]]
- [[Services/Brain/Neural/Encoders/SpreadVolatilityEncoder.cs.md|SpreadVolatilityEncoder.cs]]
- [[Services/Brain/Neural/Encoders/TraderStateEncoder.cs.md|TraderStateEncoder.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
- [[Services/Brain/Neural/ZScoreNormalization.cs.md|ZScoreNormalization.cs]]
- [[Services/Simulation/SimulationBarAggregator.cs.md|SimulationBarAggregator.cs]]
- [[Services/Training/EpisodeTimeConstraint.cs.md|EpisodeTimeConstraint.cs]]
- [[Services/Training/EpisodeWindowSampler.cs.md|EpisodeWindowSampler.cs]]
- [[Services/Training/Interfaces/ITrainingService.cs.md|ITrainingService.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
