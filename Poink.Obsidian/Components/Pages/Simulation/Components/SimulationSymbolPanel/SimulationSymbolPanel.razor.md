---
tags: [component, feature/simulation, kind/component]
role: component
feature: Simulation
feature_source: folder
kind: component
code_lines: 72
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationSymbolPanel/SimulationSymbolPanel.razor
---
# SimulationSymbolPanel.razor

The Simulation page's symbol tab: lists every ticks_&lt;symbol&gt; table in the database and keeps an ordered selection whose first entry is the traded symbol and the rest watched symbols, flagging any entry that differs from the running simulation as waiting on SET.

**Feature:** Simulation · **Kind:** Razor component · **Code lines:** 72 · **Used by:** 3

## Types
- `SimulationSymbolPanel` — component, Razor component · empty

## Uses
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
