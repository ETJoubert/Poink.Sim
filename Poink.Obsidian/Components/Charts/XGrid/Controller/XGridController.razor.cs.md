---
tags: [code-behind, feature/xgrid, kind/component-code]
role: code-behind
feature: XGrid
feature_source: folder
kind: component-code
code_lines: 67
types: 1
methods: 8
fan_in: 0
fan_out: 21
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Controller/XGridController.razor.cs
---
# XGridController.razor.cs

Owns the overlay/highlight state XGrid's own controls edit and render, merging in the host's Indicators when set -- this is the "Card" that pairs one XGrid with its Measurements/Differentials side panels per host (Profiler/Simulation/EpisodePlayback), which only appear while something on the grid is selected.

**Feature:** XGrid · **Kind:** component code-behind · **Code lines:** 67 · **Used by:** 0

## Types
- `XGridController` — class, Razor component · 8 methods, 22 properties, 6 fields

## Uses
- [[Components/Charts/XGrid/Controller/XGridController.razor.md|XGridController.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Display/XGridExternalDraft.cs.md|XGridExternalDraft.cs]]
- [[Components/Charts/XGrid/Display/XGridLine.cs.md|XGridLine.cs]]
- [[Components/Charts/XGrid/Display/XGridOrder.cs.md|XGridOrder.cs]]
- [[Components/Charts/XGrid/Display/XGridOrderModification.cs.md|XGridOrderModification.cs]]
- [[Components/Charts/XGrid/Display/XGridPending.cs.md|XGridPending.cs]]
- [[Components/Charts/XGrid/Display/XGridPosition.cs.md|XGridPosition.cs]]
- [[Components/Charts/XGrid/Helper/XGridDifferentialHelper.cs.md|XGridDifferentialHelper.cs]]
- [[Components/Charts/XGrid/Helper/XGridDisplaySettings.cs.md|XGridDisplaySettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridMeasurementHelper.cs.md|XGridMeasurementHelper.cs]]
- [[Components/Charts/XGrid/Helper/XGridPeakOverlaySettings.cs.md|XGridPeakOverlaySettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridSelection.cs.md|XGridSelection.cs]]
- [[Components/Charts/XGrid/Helper/XGridSpectrumSettings.cs.md|XGridSpectrumSettings.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Shared/Differentials/DifferentialModels.cs.md|DifferentialModels.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsHelper.cs.md|IndicatorsHelper.cs]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Components/Shared/Measurements/MeasurementModels.cs.md|MeasurementModels.cs]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
