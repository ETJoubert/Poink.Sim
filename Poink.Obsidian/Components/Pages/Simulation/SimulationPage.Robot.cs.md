---
tags: [class, feature/simulation, kind/component-code]
role: class
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 41
types: 1
methods: 3
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/SimulationPage.Robot.cs
---
# SimulationPage.Robot.cs

Hands the robot its options. The robot itself lives in the simulation engine; the page only keeps the engine's copy of the options current, pushing a fresh RobotOptions whenever any part of it -- switch, positioning, rules or the windows those rules read -- has changed.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 41 · **Used by:** 0

## Types
- `SimulationPage` — class, class with behaviour · 3 methods, 1 field

## Uses
- [[Components/Pages/Simulation/SimulationPage.razor.md|SimulationPage.razor]]
- [[Helpers/RobotPositionHelper.cs.md|RobotPositionHelper.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Models/RobotRuleSet.cs.md|RobotRuleSet.cs]]
