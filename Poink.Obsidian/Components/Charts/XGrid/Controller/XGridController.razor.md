---
tags: [component, feature/xgrid, kind/component]
role: component
feature: XGrid
feature_source: folder
kind: component
code_lines: 22
types: 1
methods: 0
fan_in: 8
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Controller/XGridController.razor
---
# XGridController.razor

Owns the overlay/highlight state XGrid's own controls edit and render, merging in the host's Indicators when set -- this is the "Card" that pairs one XGrid with its Measurements/Differentials side panels per host (Profiler/Simulation/EpisodePlayback), which only appear while something on the grid is selected.

**Feature:** XGrid · **Kind:** Razor component · **Code lines:** 22 · **Used by:** 8

## Types
- `XGridController` — component, Razor component · empty

## Uses
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridMeasurementHelper.cs.md|XGridMeasurementHelper.cs]]
- [[Components/Shared/Differentials/DifferentialsPanel.razor.md|DifferentialsPanel.razor]]
- [[Components/Shared/Measurements/MeasurementsPanel.razor.md|MeasurementsPanel.razor]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
