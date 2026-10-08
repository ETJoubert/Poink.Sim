---
tags: [component]
source: Poink.Sim/Components/Shared/PositionTable/PositionTable.razor
---
# PositionTable.razor

Read-only, tight-row table of open positions -- no card view or column picker, unlike the generic DataTable. Shared by the live Trade page and the Simulation page: it renders whatever rows the host hands it and round-trips TPosition back through OnSelect/OnClose, with no backend of its own.

**Used by:** 5 files (see Backlinks)

## Uses
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
