---
tags: [class]
source: Poink.Sim/Components/Pages/Simulation/SimulationPage.Watch.cs
---
# SimulationPage.Watch.cs

The watch-only symbols replayed alongside the traded one: their pip sizes are resolved before a load, since each symbol's price buckets are sized from its own pip, and their live charts are shown under the traded symbol's.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationRowCache.cs.md|SimulationRowCache.cs]]
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Components/Pages/Simulation/Models/SimulationSymbolPanelModels.cs.md|SimulationSymbolPanelModels.cs]]
- [[Components/Pages/Simulation/SimulationPage.razor.md|SimulationPage.razor]]
- [[Helpers/SymbolColorHelper.cs.md|SymbolColorHelper.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Models/SimulationWatchModels.cs.md|SimulationWatchModels.cs]]
- [[Services/Symbols/SymbolTableResolver.cs.md|SymbolTableResolver.cs]]
