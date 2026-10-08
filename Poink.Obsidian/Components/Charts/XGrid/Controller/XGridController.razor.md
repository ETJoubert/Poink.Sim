---
tags: [component]
source: Poink.Sim/Components/Charts/XGrid/Controller/XGridController.razor
---
# XGridController.razor

Owns the overlay/highlight state XGrid's own controls edit and render, merging in the host's Indicators when set -- this is the "Card" that pairs one XGrid with its Measurements/Differentials side panels per host (Profiler/Simulation/EpisodePlayback), which only appear while something on the grid is selected.

**Used by:** 8 files (see Backlinks)

## Uses
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridMeasurementHelper.cs.md|XGridMeasurementHelper.cs]]
- [[Components/Shared/Differentials/DifferentialsPanel.razor.md|DifferentialsPanel.razor]]
- [[Components/Shared/Measurements/MeasurementsPanel.razor.md|MeasurementsPanel.razor]]
- [[Models/TickRateModels.cs.md|TickRateModels.cs]]
