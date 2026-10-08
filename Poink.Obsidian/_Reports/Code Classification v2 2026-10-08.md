---
tags: [report]
version: 2
date: 2026-10-08
---
# Code classification v2 — 2026-10-08

Scope: 896 Poink.Sim files (604 .cs, 139 .razor, 137 .css, 16 .js). Generated code is excluded: EF migrations, `*.Designer.cs`, model snapshots, minified and vendor libraries.

## At a glance

| Measure | Value |
|---|---|
| Files | 896 |
| Links | 2951 |
| Declared C# types | 1081 |
| Files with their own summary doc | 467 (52%) |
| Files with at least one candidate | 88 (10%) |

## File kinds

| Kind | Files | Share | Median code lines |
|---|---|---|---|
| component code-behind | 136 | 15% | 65 |
| scoped stylesheet | 131 | 15% | 74 |
| class with behaviour | 127 | 14% | 66 |
| Razor component | 125 | 14% | 37 |
| interface | 72 | 8% | 10 |
| data type (at most one helper method) | 69 | 8% | 13 |
| mixed types | 55 | 6% | 54 |
| bundle of data types | 53 | 6% | 27 |
| static helper | 46 | 5% | 38 |
| single-method class | 38 | 4% | 20 |
| script | 16 | 2% | 146 |
| Razor page | 14 | 2% | 210 |
| stylesheet | 6 | 1% | 277 |
| enum | 5 | 1% | 6 |
| static constants | 2 | 0% | 46 |
| no type declarations | 1 | 0% | 59 |

## C# types by shape

Counts every declared type, nested ones included.

| Shape | Types | Share |
|---|---|---|
| data type (at most one helper method) | 470 | 43% |
| class with behaviour | 156 | 14% |
| Razor component | 130 | 12% |
| enum | 109 | 10% |
| interface | 87 | 8% |
| static helper | 73 | 7% |
| single-method class | 46 | 4% |
| static constants | 10 | 1% |

## Features

| Feature | Files | By folder | By usage | With candidates |
|---|---|---|---|---|
| Shared | 176 | 0 | 0 | 15 |
| Training | 122 | 109 | 13 | 15 |
| Trade | 63 | 60 | 3 | 3 |
| Brain | 57 | 57 | 0 | 8 |
| Emulator | 51 | 43 | 8 | 8 |
| Simulation | 49 | 47 | 2 | 3 |
| XGrid | 36 | 35 | 1 | 13 |
| TrainingEngine | 29 | 27 | 2 | 9 |
| Trader | 24 | 24 | 0 | 1 |
| Profiler | 22 | 21 | 1 | 3 |
| SignalStudy | 20 | 16 | 4 | 2 |
| Arch | 19 | 19 | 0 | 1 |
| MarketData | 19 | 19 | 0 | 1 |
| Code | 17 | 17 | 0 | 0 |
| Layout | 16 | 15 | 1 | 1 |
| CTrader | 16 | 14 | 2 | 0 |
| MarketChart | 15 | 13 | 2 | 0 |
| TradeMap | 14 | 14 | 0 | 0 |
| Analize | 13 | 11 | 2 | 0 |
| OrderBot | 11 | 11 | 0 | 3 |
| Activity | 10 | 10 | 0 | 0 |
| Analysis | 9 | 9 | 0 | 0 |
| TxFold | 8 | 6 | 2 | 1 |
| Symbols | 7 | 7 | 0 | 0 |
| CompareTxFold | 6 | 5 | 1 | 0 |
| CompareXGrid | 6 | 5 | 1 | 0 |
| EquityCurve | 6 | 5 | 1 | 1 |
| Calls | 6 | 6 | 0 | 0 |
| RuleEngine | 6 | 6 | 0 | 0 |
| CompareChart | 5 | 4 | 1 | 0 |
| CompareSet | 5 | 5 | 0 | 0 |
| CandlestickChart | 4 | 3 | 1 | 0 |
| TickVolumeProfileChart | 4 | 4 | 0 | 0 |
| Bots | 4 | 4 | 0 | 0 |
| DataSeriesSyncChart | 3 | 3 | 0 | 0 |
| HistogramChart | 3 | 3 | 0 | 0 |
| TickTimeProfileChart | 3 | 3 | 0 | 0 |
| Data | 3 | 3 | 0 | 0 |
| DataSeriesSync | 3 | 3 | 0 | 0 |
| Diagnostics | 3 | 3 | 0 | 0 |
| Registration | 2 | 2 | 0 | 0 |
| Charts | 1 | 1 | 0 | 0 |

## Candidates

Heuristic findings to review, not verdicts. Each file's note repeats its own candidates.

### Small data files that could share one *Models file (37)

| File | Folder | Feature | Lines | Used by | Suggestion |
|---|---|---|---|---|---|
| [[Components/Charts/XGrid/Display/XGridExternalDraft.cs.md|XGridExternalDraft.cs]] | Components/Charts/XGrid/Display | XGrid | 2 | 4 | Merge into a new `XGridModels.cs` with `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`. |
| [[Components/Charts/XGrid/Display/XGridOrder.cs.md|XGridOrder.cs]] | Components/Charts/XGrid/Display | XGrid | 8 | 3 | Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`. |
| [[Components/Charts/XGrid/Display/XGridOrderModification.cs.md|XGridOrderModification.cs]] | Components/Charts/XGrid/Display | XGrid | 9 | 3 | Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`. |
| [[Components/Charts/XGrid/Display/XGridPending.cs.md|XGridPending.cs]] | Components/Charts/XGrid/Display | XGrid | 9 | 4 | Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPosition.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`. |
| [[Components/Charts/XGrid/Display/XGridPosition.cs.md|XGridPosition.cs]] | Components/Charts/XGrid/Display | XGrid | 15 | 6 | Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridReadout.cs`, `XGridTooltipRow.cs`. |
| [[Components/Charts/XGrid/Display/XGridReadout.cs.md|XGridReadout.cs]] | Components/Charts/XGrid/Display | XGrid | 2 | 2 | Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridTooltipRow.cs`. |
| [[Components/Charts/XGrid/Display/XGridTooltipRow.cs.md|XGridTooltipRow.cs]] | Components/Charts/XGrid/Display | XGrid | 7 | 1 | Merge into a new `XGridModels.cs` with `XGridExternalDraft.cs`, `XGridOrder.cs`, `XGridOrderModification.cs`, `XGridPending.cs`, `XGridPosition.cs`, `XGridReadout.cs`. |
| [[Components/Charts/XGrid/Helper/XGridDisplaySettings.cs.md|XGridDisplaySettings.cs]] | Components/Charts/XGrid/Helper | XGrid | 23 | 4 | Merge into a new `XGridModels.cs` with `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`. |
| [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]] | Components/Charts/XGrid/Helper | XGrid | 4 | 4 | Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`. |
| [[Components/Charts/XGrid/Helper/XGridPeakOverlaySettings.cs.md|XGridPeakOverlaySettings.cs]] | Components/Charts/XGrid/Helper | XGrid | 8 | 3 | Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`. |
| [[Components/Charts/XGrid/Helper/XGridSelection.cs.md|XGridSelection.cs]] | Components/Charts/XGrid/Helper | XGrid | 11 | 4 | Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSpectrumSettings.cs`, `XGridSweepMode.cs`. |
| [[Components/Charts/XGrid/Helper/XGridSpectrumSettings.cs.md|XGridSpectrumSettings.cs]] | Components/Charts/XGrid/Helper | XGrid | 15 | 3 | Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSweepMode.cs`. |
| [[Components/Charts/XGrid/Helper/XGridSweepMode.cs.md|XGridSweepMode.cs]] | Components/Charts/XGrid/Helper | XGrid | 6 | 2 | Merge into a new `XGridModels.cs` with `XGridDisplaySettings.cs`, `XGridDwellComparison.cs`, `XGridPeakOverlaySettings.cs`, `XGridSelection.cs`, `XGridSpectrumSettings.cs`. |
| [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]] | Models | Shared | 6 | 21 | Merge into a new `SharedModels.cs` with `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`. |
| [[Models/BotAccountOptions.cs.md|BotAccountOptions.cs]] | Models | Shared | 11 | 5 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`. |
| [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]] | Models | Shared | 17 | 42 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`. |
| [[Models/QuartileBar.cs.md|QuartileBar.cs]] | Models | Shared | 12 | 7 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`. |
| [[Models/RobotPositionRules.cs.md|RobotPositionRules.cs]] | Models | Shared | 24 | 8 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`. |
| [[Models/RobotRuleSet.cs.md|RobotRuleSet.cs]] | Models | Shared | 16 | 5 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RuleSignalSettings.cs`, `StatGroup.cs`. |
| [[Models/RuleSignalSettings.cs.md|RuleSignalSettings.cs]] | Models | Shared | 23 | 7 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `StatGroup.cs`. |
| [[Models/StatGroup.cs.md|StatGroup.cs]] | Models | Shared | 8 | 5 | Merge into a new `SharedModels.cs` with `BarAggregationKind.cs`, `BotAccountOptions.cs`, `HlocAvdtBar.cs`, `QuartileBar.cs`, `RobotPositionRules.cs`, `RobotRuleSet.cs`, `RuleSignalSettings.cs`. |
| [[Models/TrainPageSettings.cs.md|TrainPageSettings.cs]] | Models | Training | 25 | 1 | Merge into `TrainingResultModels.cs`. |
| [[Models/TrainingModels/DataOptionsModel.cs.md|DataOptionsModel.cs]] | Models/TrainingModels | Training | 3 | 3 | Merge into `TrainingStrategyModels.cs` with `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`. |
| [[Models/TrainingModels/FeatureModel.cs.md|FeatureModel.cs]] | Models/TrainingModels | Training | 12 | 3 | Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`. |
| [[Models/TrainingModels/FeatureOptionsModel.cs.md|FeatureOptionsModel.cs]] | Models/TrainingModels | Training | 27 | 3 | Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`. |
| [[Models/TrainingModels/FeatureVector.cs.md|FeatureVector.cs]] | Models/TrainingModels | Training | 2 | 2 | Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`. |
| [[Models/TrainingModels/NetworkOptionsModel.cs.md|NetworkOptionsModel.cs]] | Models/TrainingModels | Training | 12 | 3 | Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`. |
| [[Models/TrainingModels/OutputOptionsModel.cs.md|OutputOptionsModel.cs]] | Models/TrainingModels | Training | 21 | 4 | Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `TrainingOptionsModel.cs`. |
| [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]] | Models/TrainingModels | Training | 9 | 6 | Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`. |
| [[Services/OrderBot/OrderBotOptions.cs.md|OrderBotOptions.cs]] | Services/OrderBot | OrderBot | 9 | 6 | Merge into `OrderBotModels.cs`. |
| [[Services/TrainingEngine/DecisionTrace.cs.md|DecisionTrace.cs]] | Services/TrainingEngine | TrainingEngine | 3 | 3 | Merge into `TrainingRunModels.cs` with `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`. |
| [[Services/TrainingEngine/EpisodeData.cs.md|EpisodeData.cs]] | Services/TrainingEngine | TrainingEngine | 6 | 1 | Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`. |
| [[Services/TrainingEngine/EpisodeResult.cs.md|EpisodeResult.cs]] | Services/TrainingEngine | TrainingEngine | 7 | 1 | Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`. |
| [[Services/TrainingEngine/LiveEpisodeSnapshot.cs.md|LiveEpisodeSnapshot.cs]] | Services/TrainingEngine | TrainingEngine | 28 | 3 | Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`. |
| [[Services/TrainingEngine/LiveTickSnapshot.cs.md|LiveTickSnapshot.cs]] | Services/TrainingEngine | TrainingEngine | 18 | 3 | Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`. |
| [[Services/TrainingEngine/LoadedTrainedModel.cs.md|LoadedTrainedModel.cs]] | Services/TrainingEngine | TrainingEngine | 3 | 1 | Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `TraderStateSnapshot.cs`. |
| [[Services/TrainingEngine/TraderStateSnapshot.cs.md|TraderStateSnapshot.cs]] | Services/TrainingEngine | TrainingEngine | 3 | 2 | Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`. |

### Single-method classes (16)

| File | Folder | Feature | Lines | Used by | Suggestion |
|---|---|---|---|---|---|
| [[Components/Charts/EquityCurve/EquityCurveHelper.cs.md|EquityCurveHelper.cs]] | Components/Charts/EquityCurve | EquityCurve | 50 | 2 | `Build` is used by 2 files; check it earns its own type. |
| [[Components/Pages/Arch/Components/ArchAreaPalette.cs.md|ArchAreaPalette.cs]] | Components/Pages/Arch/Components | Arch | 9 | 1 | Inline `ColorFor` into its only user `ArchPage.razor`. |
| [[Components/Pages/Profiler/Helpers/ProfilerFormatHelper.cs.md|ProfilerFormatHelper.cs]] | Components/Pages/Profiler/Helpers | Profiler | 9 | 1 | Inline `FormatSpan` into its only user `ProfilerPage.razor`. |
| [[Helpers/BarSizeOptions.cs.md|BarSizeOptions.cs]] | Helpers | Shared | 31 | 2 | Fold `Label` into a sibling helper (`ChartBarRowHelper.cs`, `DifferentialHelper.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorTickHelper.cs`, `IndicatorRowsHelper.cs`, `MarketRegimeHelper.cs`, `SplitTickReadHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`). |
| [[Helpers/EmulatorGroupColorHelper.cs.md|EmulatorGroupColorHelper.cs]] | Helpers | Emulator | 4 | 3 | Fold `ColorVar` into a sibling helper (`BarSizeOptions.cs`, `ChartBarRowHelper.cs`, `DifferentialHelper.cs`, `EmulatorTickHelper.cs`, `IndicatorRowsHelper.cs`, `MarketRegimeHelper.cs`, `SplitTickReadHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`). |
| [[Helpers/TxFoldBarsCache.cs.md|TxFoldBarsCache.cs]] | Helpers | Shared | 41 | 2 | `RefreshIfNeededAsync` is used by 2 files; check it earns its own type. |
| [[Services/Brain/Neural/BinaryNormalization.cs.md|BinaryNormalization.cs]] | Services/Brain/Neural | Brain | 8 | 3 | Fold `Compute` into a sibling helper (`CategoricalSampling.cs`, `ZScoreNormalization.cs`). |
| [[Services/Brain/Neural/ActionMasking/DirectionActionMasker.cs.md|DirectionActionMasker.cs]] | Services/Brain/Neural/ActionMasking | Brain | 11 | 2 | `Mask` is used by 2 files; check it earns its own type. |
| [[Services/Brain/Neural/DecisionMasking/DecisionMasker.cs.md|DecisionMasker.cs]] | Services/Brain/Neural/DecisionMasking | Brain | 38 | 2 | `ShouldDecide` is used by 2 files; check it earns its own type. |
| [[Services/Brain/Neural/Neat/NeatCrossoverService.cs.md|NeatCrossoverService.cs]] | Services/Brain/Neural/Neat | Brain | 41 | 1 | Inline `Crossover` into its only user `NeatPopulation.cs`. |
| [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]] | Services/MarketData | MarketData | 8 | 9 | `ForSymbol` is used by 9 files; check it earns its own type. |
| [[Services/OrderBot/OrderBotRoutingHelper.cs.md|OrderBotRoutingHelper.cs]] | Services/OrderBot | OrderBot | 14 | 2 | Fold `TryGetBotAccount` into a sibling helper (`OrderBotStatusHelper.cs`). |
| [[Services/OrderBot/OrderBotStatusHelper.cs.md|OrderBotStatusHelper.cs]] | Services/OrderBot | OrderBot | 20 | 1 | Inline `Evaluate` into its only user `OrderBotStatusMonitor.cs`. |
| [[Services/Simulation/DummyTickGenerator.cs.md|DummyTickGenerator.cs]] | Services/Simulation | Simulation | 20 | 1 | Inline `Generate` into its only user `TrainingService.cs`. |
| [[Services/TrainingEngine/DecisionMask.cs.md|DecisionMask.cs]] | Services/TrainingEngine | TrainingEngine | 22 | 1 | Inline `ShouldDecide` into its only user `TrainingEngineService.cs`. |
| [[Services/TrainingEngine/LiveBarBuilder.cs.md|LiveBarBuilder.cs]] | Services/TrainingEngine | TrainingEngine | 22 | 1 | Inline `Feed` into its only user `TrainingEngineService.cs`. |

### Single-method services behind an interface (4)

| File | Folder | Feature | Lines | Used by | Suggestion |
|---|---|---|---|---|---|
| [[Services/Brain/Neural/DecisionMasking/PriceBelowPocRule.cs.md|PriceBelowPocRule.cs]] | Services/Brain/Neural/DecisionMasking | Brain | 8 | 1 | Only `IsSatisfied` behind `IDecisionMaskRule`, its sole implementation; fold into a broader service unless it is a deliberate seam. |
| [[Services/Profiler/ProfilerService.cs.md|ProfilerService.cs]] | Services/Profiler | Profiler | 67 | 3 | Only `LoadAsync` behind `IProfilerService`, its sole implementation; fold into a broader service unless it is a deliberate seam. |
| [[Services/Trader/Commission/PercentOfNotionalCommission.cs.md|PercentOfNotionalCommission.cs]] | Services/Trader/Commission | Trader | 5 | 5 | Only `CommissionFor` behind `ICommissionModel`, its sole implementation; fold into a broader service unless it is a deliberate seam. |
| [[Services/Training/RewardPolicies/RewardPolicyService.cs.md|RewardPolicyService.cs]] | Services/Training/RewardPolicies | Training | 25 | 3 | Only `SelectPolicy` behind `IRewardPolicyService`, its sole implementation; fold into a broader service unless it is a deliberate seam. |

### Folders with many small helpers (12)

| File | Folder | Feature | Lines | Used by | Suggestion |
|---|---|---|---|---|---|
| [[Helpers/BarSizeOptions.cs.md|BarSizeOptions.cs]] | Helpers | Shared | 31 | 2 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/EmulatorGroupColorHelper.cs.md|EmulatorGroupColorHelper.cs]] | Helpers | Emulator | 4 | 3 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/EmulatorGroupRankingHelper.cs.md|EmulatorGroupRankingHelper.cs]] | Helpers | Emulator | 19 | 1 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/EmulatorMarketScaleHelper.cs.md|EmulatorMarketScaleHelper.cs]] | Helpers | Emulator | 42 | 3 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/IndicatorRowsHelper.cs.md|IndicatorRowsHelper.cs]] | Helpers | Simulation | 21 | 1 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/SymbolColorHelper.cs.md|SymbolColorHelper.cs]] | Helpers | Shared | 20 | 8 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/TickRateHelper.cs.md|TickRateHelper.cs]] | Helpers | Shared | 28 | 2 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickTableRangeHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/TickTableRangeHelper.cs.md|TickTableRangeHelper.cs]] | Helpers | Shared | 8 | 3 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `VectorStatsHelper.cs`. |
| [[Helpers/VectorStatsHelper.cs.md|VectorStatsHelper.cs]] | Helpers | Shared | 21 | 1 | One of 9 small static helpers in `Helpers`; consolidate by purpose with `BarSizeOptions.cs`, `EmulatorGroupColorHelper.cs`, `EmulatorGroupRankingHelper.cs`, `EmulatorMarketScaleHelper.cs`, `IndicatorRowsHelper.cs`, `SymbolColorHelper.cs`, `TickRateHelper.cs`, `TickTableRangeHelper.cs`. |
| [[Services/Brain/Neural/Neat/NeatCrossoverService.cs.md|NeatCrossoverService.cs]] | Services/Brain/Neural/Neat | Brain | 41 | 1 | One of 3 small static helpers in `Services/Brain/Neural/Neat`; consolidate by purpose with `NeatNetwork.cs`, `NeatSpeciationService.cs`. |
| [[Services/Brain/Neural/Neat/NeatNetwork.cs.md|NeatNetwork.cs]] | Services/Brain/Neural/Neat | Brain | 58 | 1 | One of 3 small static helpers in `Services/Brain/Neural/Neat`; consolidate by purpose with `NeatCrossoverService.cs`, `NeatSpeciationService.cs`. |
| [[Services/Brain/Neural/Neat/NeatSpeciationService.cs.md|NeatSpeciationService.cs]] | Services/Brain/Neural/Neat | Brain | 57 | 1 | One of 3 small static helpers in `Services/Brain/Neural/Neat`; consolidate by purpose with `NeatCrossoverService.cs`, `NeatNetwork.cs`. |

### Shared/core files used by only one feature (25)

| File | Folder | Feature | Lines | Used by | Suggestion |
|---|---|---|---|---|---|
| [[Components/Shared/PageNav/PageNavState.cs.md|PageNavState.cs]] | Components/Shared/PageNav | Layout | 6 | 1 | Used only by the Layout feature; move it into that feature's folder. |
| [[Helpers/BarAggregator.cs.md|BarAggregator.cs]] | Helpers | TxFold | 54 | 1 | Used only by the TxFold feature; move it into that feature's folder. |
| [[Helpers/ChartBarRowHelper.cs.md|ChartBarRowHelper.cs]] | Helpers | Profiler | 23 | 2 | Used only by the Profiler feature; move it into that feature's folder. |
| [[Helpers/EmulatorChartHelper.cs.md|EmulatorChartHelper.cs]] | Helpers | Emulator | 82 | 11 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Helpers/EmulatorGroupColorHelper.cs.md|EmulatorGroupColorHelper.cs]] | Helpers | Emulator | 4 | 3 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Helpers/EmulatorGroupRankingHelper.cs.md|EmulatorGroupRankingHelper.cs]] | Helpers | Emulator | 19 | 1 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Helpers/EmulatorLadderHelper.cs.md|EmulatorLadderHelper.cs]] | Helpers | Emulator | 57 | 1 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Helpers/EmulatorMarketScaleHelper.cs.md|EmulatorMarketScaleHelper.cs]] | Helpers | Emulator | 42 | 3 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Helpers/EmulatorTickHelper.cs.md|EmulatorTickHelper.cs]] | Helpers | Emulator | 6 | 3 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Helpers/FeatureDisplayFormatter.cs.md|FeatureDisplayFormatter.cs]] | Helpers | Training | 67 | 3 | Used only by the Training feature; move it into that feature's folder. |
| [[Helpers/IndicatorRowsHelper.cs.md|IndicatorRowsHelper.cs]] | Helpers | Simulation | 21 | 1 | Used only by the Simulation feature; move it into that feature's folder. |
| [[Helpers/ISessionStorageHelper.cs.md|ISessionStorageHelper.cs]] | Helpers | Trade | 5 | 3 | Used only by the Trade feature; move it into that feature's folder. |
| [[Helpers/SessionStorageHelper.cs.md|SessionStorageHelper.cs]] | Helpers | Trade | 6 | 1 | Used only by the Trade feature; move it into that feature's folder. |
| [[Models/EmulatorChartMode.cs.md|EmulatorChartMode.cs]] | Models | Emulator | 4 | 5 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Models/EmulatorPageSettings.cs.md|EmulatorPageSettings.cs]] | Models | Emulator | 45 | 1 | Used only by the Emulator feature; move it into that feature's folder. |
| [[Models/NeuralNetworkModels.cs.md|NeuralNetworkModels.cs]] | Models | Training | 9 | 6 | Used only by the Training feature; move it into that feature's folder. |
| [[Models/NeuralWeightsModels.cs.md|NeuralWeightsModels.cs]] | Models | Training | 17 | 5 | Used only by the Training feature; move it into that feature's folder. |
| [[Models/SignalAnalysisModels.cs.md|SignalAnalysisModels.cs]] | Models | SignalStudy | 45 | 9 | Used only by the SignalStudy feature; move it into that feature's folder. |
| [[Models/SignalStudyModels.cs.md|SignalStudyModels.cs]] | Models | SignalStudy | 63 | 12 | Used only by the SignalStudy feature; move it into that feature's folder. |
| [[Models/SimulationFinancialsModels.cs.md|SimulationFinancialsModels.cs]] | Models | Simulation | 12 | 3 | Used only by the Simulation feature; move it into that feature's folder. |
| [[Models/TradePageSettings.cs.md|TradePageSettings.cs]] | Models | Trade | 26 | 2 | Used only by the Trade feature; move it into that feature's folder. |
| [[Models/TrainingResultModels.cs.md|TrainingResultModels.cs]] | Models | Training | 62 | 4 | Used only by the Training feature; move it into that feature's folder. |
| [[Models/TrainingSaveModels.cs.md|TrainingSaveModels.cs]] | Models | Training | 12 | 3 | Used only by the Training feature; move it into that feature's folder. |
| [[Models/TrainingStartModels.cs.md|TrainingStartModels.cs]] | Models | Training | 18 | 3 | Used only by the Training feature; move it into that feature's folder. |
| [[Models/TrainPageSettings.cs.md|TrainPageSettings.cs]] | Models | Training | 25 | 1 | Used only by the Training feature; move it into that feature's folder. |

### Files nothing references (2)

| File | Folder | Feature | Lines | Used by | Suggestion |
|---|---|---|---|---|---|
| [[Services/Brain/Neural/Normalizers/EquityRatioNormalizer.cs.md|EquityRatioNormalizer.cs]] | Services/Brain/Neural/Normalizers | Brain | 9 | 0 | Nothing in Poink.Sim references it; confirm it is still needed. |
| [[wwwroot/js/networkEmulator.js.md|networkEmulator.js]] | wwwroot/js | Shared | 146 | 0 | Nothing in Poink.Sim references it; confirm it is still needed. |

## Guideline observations

- **One type per file for small data (37 files, 6 folders).** CLAUDE.md §2.3 names `*ThingModels` as the home for models, DTOs and records but never says when to merge. A rule such as *“records, enums and DTOs under 40 lines live in the feature's `<Feature>Models.cs`”* would stop new one-record files.
- **Single-method classes (16, 7 with exactly one user).** SOLID is being read as “one method per class”. A rule such as *“a class with one method and one caller is a private method on that caller unless it is injected or reused”* keeps SRP without the file count.
- **One-operation services behind interfaces (4).** The DI rule in §2.1 is met, but each operation gets its own interface, implementation and registration. Consider grouping related operations per feature service.
- **Helper sprawl (12 files in 2 folders).** §2.2 asks for helpers grouped by purpose; a rule like *“one `ThingHelper` per purpose per feature, extend it before adding a new one”* makes that concrete.
- **Shared/core code used by one feature (25).** A rule such as *“only code used by two or more features goes into Shared, Models, Helpers or Repository”* keeps features self-contained.
- **Unreferenced files (2).** Check before deleting: DI by name, reflection or JS interop can hide usage.
- **Summaries (429 files, 48%, have none of their own).** §4 already permits XML docs on public members; requiring a `<summary>` on each top-level type improves this map and gives the model a cheaper way to navigate than reading whole files.

## Using this in Obsidian

- Graph colours are by feature; grey is Shared. Reports are filtered out of the graph.
- Graph filter `tag:#candidate` shows only files with findings; `tag:#candidate/merge-data` narrows to one kind.
- Graph filter `tag:#feature/trade` isolates one feature; `-tag:#feature/shared` hides shared code.
- Each note's properties (`kind`, `code_lines`, `fan_in`, `fan_out`, `candidates`) can be sorted in a Bases table.
