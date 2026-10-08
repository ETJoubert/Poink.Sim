---
tags: [class]
source: Poink.Sim/Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs
---
# SimulationSymbolPanelBuilder.cs

Computes a watched symbol's chart set for the traded symbol's Lookback Window, applied by time so every symbol covers the same span the way the Profiler aligns its symbols; recomputes only when the symbol's bars or a setting changed.

**Used by:** 20 files (see Backlinks)

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationRowCache.cs.md|SimulationRowCache.cs]]
- [[Components/Pages/Simulation/Models/SimulationSymbolPanelModels.cs.md|SimulationSymbolPanelModels.cs]]
- [[Components/Shared/Indicators/IndicatorChartOverlayHelper.cs.md|IndicatorChartOverlayHelper.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Helpers/TickRateHelper.cs.md|TickRateHelper.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationWatchModels.cs.md|SimulationWatchModels.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
