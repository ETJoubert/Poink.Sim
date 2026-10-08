---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 109
types: 1
methods: 7
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/PositionTable/PositionTable.razor.cs
---
# PositionTable.razor.cs

Read-only, tight-row table of open positions -- no card view or column picker, unlike the generic DataTable. Shared by the live Trade page and the Simulation page: it renders whatever rows the host hands it and round-trips TPosition back through OnSelect/OnClose, with no backend of its own.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 109 · **Used by:** 0

## Types
- `PositionTable` — class, Razor component · 7 methods, 7 properties

## Uses
- [[Components/Shared/PositionTable/PositionRowFormat.cs.md|PositionRowFormat.cs]]
- [[Components/Shared/PositionTable/PositionTable.razor.md|PositionTable.razor]]
- [[Components/Shared/PositionTable/PositionTableModels.cs.md|PositionTableModels.cs]]
