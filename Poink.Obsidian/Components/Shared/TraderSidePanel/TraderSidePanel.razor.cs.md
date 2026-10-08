---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 25
types: 2
methods: 1
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/TraderSidePanel/TraderSidePanel.razor.cs
---
# TraderSidePanel.razor.cs

Tabbed shell for a page's trader-controls sidebar -- Trade/Policies/Robot -- shared by the live Trade page and the Simulation page so both present the same sidebar shape.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 25 · **Used by:** 1

## Types
- `TraderSidePanel` — class, Razor component · 1 method, 6 properties, 1 field
- `SidePanelTab` — enum, nested, enum · 3 values

## Uses
- [[Components/Shared/TraderSidePanel/TraderSidePanel.razor.md|TraderSidePanel.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
