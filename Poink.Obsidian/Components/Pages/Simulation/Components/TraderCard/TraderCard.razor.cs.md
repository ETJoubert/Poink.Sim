---
tags: [code-behind, feature/simulation, kind/component-code]
role: code-behind
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 29
types: 1
methods: 1
fan_in: 0
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/TraderCard/TraderCard.razor.cs
---
# TraderCard.razor.cs

One trader's live figures as a card, for the Simulation page's Traders tab -- click to select, same as the table's old row click did. Takes plain values rather than the internal TraderSnapshot itself, since a public component parameter can't expose an internal type.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 29 · **Used by:** 0

## Types
- `TraderCard` — class, Razor component · 1 method, 12 properties

## Uses
- [[Components/Pages/Simulation/Components/TraderCard/TraderCard.razor.md|TraderCard.razor]]
