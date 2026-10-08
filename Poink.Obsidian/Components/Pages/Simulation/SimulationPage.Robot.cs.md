---
tags: [class]
source: Poink.Sim/Components/Pages/Simulation/SimulationPage.Robot.cs
---
# SimulationPage.Robot.cs

Hands the robot its options. The robot itself lives in the simulation engine; the page only keeps the engine's copy of the options current, pushing a fresh RobotOptions whenever any part of it -- switch, positioning, rules or the windows those rules read -- has changed.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Pages/Simulation/SimulationPage.razor.md|SimulationPage.razor]]
- [[Helpers/RobotPositionHelper.cs.md|RobotPositionHelper.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Models/RobotRuleSet.cs.md|RobotRuleSet.cs]]
