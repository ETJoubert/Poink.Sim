---
tags: [code-behind, feature/simulation, kind/component-code]
role: code-behind
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 30
types: 1
methods: 2
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/Components/SimulationEquityCurve/SimulationEquityCurve.razor.cs
---
# SimulationEquityCurve.razor.cs

One trader's equity curve, fed straight from the simulation engine and re-rendered on its own, so it keeps updating at max speed while the rest of the Simulation page does not render.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 30 · **Used by:** 0

## Types
- `SimulationEquityCurve` — class, Razor component · 2 methods, 3 properties, 2 fields

## Uses
- [[Components/Charts/EquityCurve/EquityCurveSample.cs.md|EquityCurveSample.cs]]
- [[Components/Pages/Simulation/Components/SimulationEquityCurve/SimulationEquityCurve.razor.md|SimulationEquityCurve.razor]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Services/Simulation/ISimulationEngine.cs.md|ISimulationEngine.cs]]
