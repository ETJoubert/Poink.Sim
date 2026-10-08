---
tags: [component, feature/simulation, kind/component]
role: component
feature: Simulation
feature_source: folder
kind: component
code_lines: 38
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/TraderCard/TraderCard.razor
---
# TraderCard.razor

One trader's live figures as a card, for the Simulation page's Traders tab -- click to select, same as the table's old row click did. Takes plain values rather than the internal TraderSnapshot itself, since a public component parameter can't expose an internal type.

**Feature:** Simulation · **Kind:** Razor component · **Code lines:** 38 · **Used by:** 3

## Types
- `TraderCard` — component, Razor component · empty

## Uses
- [[Components/Shared/FinancialsPanel/FinancialsPanel.razor.md|FinancialsPanel.razor]]
