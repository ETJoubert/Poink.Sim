---
tags: [class]
source: Poink.Sim/Components/Pages/Simulation/Helpers/SimulationRowCache.cs
---
# SimulationRowCache.cs

Each symbol's chart and tick-table rows, rebuilt only when the engine publishes that symbol's bars or ticks anew, so an unchanged symbol costs no conversion per snapshot and keeps handing out the same list.

**Used by:** 6 files (see Backlinks)

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationRowHelper.cs.md|SimulationRowHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
