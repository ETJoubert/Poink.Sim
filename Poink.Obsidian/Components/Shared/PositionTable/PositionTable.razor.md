---
tags: [component, feature/shared, kind/component]
role: component
feature: Shared
feature_source: shared
kind: component
code_lines: 35
types: 1
methods: 0
fan_in: 10
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/PositionTable/PositionTable.razor
---
# PositionTable.razor

Read-only, tight-row table of open positions -- no card view or column picker, unlike the generic DataTable. Shared by the live Trade page and the Simulation page: it renders whatever rows the host hands it and round-trips TPosition back through OnSelect/OnClose, with no backend of its own.

**Feature:** Shared · **Kind:** Razor component · **Code lines:** 35 · **Used by:** 10

## Types
- `PositionTable` — component, Razor component · empty

## Uses
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
