---
tags: [code-behind]
source: Poink.Sim/Components/Shared/PositionTable/PositionTable.razor.cs
---
# PositionTable.razor.cs

Read-only, tight-row table of open positions -- no card view or column picker, unlike the generic DataTable. Shared by the live Trade page and the Simulation page: it renders whatever rows the host hands it and round-trips TPosition back through OnSelect/OnClose, with no backend of its own.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationRowCache.cs.md|SimulationRowCache.cs]]
- [[Components/Shared/PositionTable/PositionRowFormat.cs.md|PositionRowFormat.cs]]
- [[Components/Shared/PositionTable/PositionTable.razor.md|PositionTable.razor]]
- [[Components/Shared/PositionTable/PositionTableModels.cs.md|PositionTableModels.cs]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
