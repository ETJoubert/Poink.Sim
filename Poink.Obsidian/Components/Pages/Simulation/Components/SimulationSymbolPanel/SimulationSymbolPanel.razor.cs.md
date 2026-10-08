---
tags: [code-behind, feature/simulation, kind/component-code]
role: code-behind
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 68
types: 1
methods: 11
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationSymbolPanel/SimulationSymbolPanel.razor.cs
---
# SimulationSymbolPanel.razor.cs

The Simulation page's symbol tab: lists every ticks_&lt;symbol&gt; table in the database and keeps an ordered selection whose first entry is the traded symbol and the rest watched symbols, flagging any entry that differs from the running simulation as waiting on SET.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 68 · **Used by:** 0

## Types
- `SimulationSymbolPanel` — class, Razor component · 11 methods, 8 properties, 4 fields

## Uses
- [[Components/Pages/Simulation/Components/SimulationSymbolPanel/SimulationSymbolPanel.razor.md|SimulationSymbolPanel.razor]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
