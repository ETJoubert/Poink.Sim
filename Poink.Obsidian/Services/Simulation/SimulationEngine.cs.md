---
tags: [class, feature/simulation, kind/behavior]
role: class
feature: Simulation
feature_source: folder
kind: behavior
code_lines: 672
types: 2
methods: 46
fan_in: 1
fan_out: 19
summary_source: documented
candidates: []
source: Poink.Sim/Services/Simulation/SimulationEngine.cs
---
# SimulationEngine.cs

Interactive replay driver for one shared, in-process simulation run: plays, pauses and steps ticks through the shared aggregator and trader, and publishes an immutable snapshot after each callback for the UI to render.

**Feature:** Simulation · **Kind:** class with behaviour · **Code lines:** 672 · **Used by:** 1

## Types
- `SimulationEngine` — class, class with behaviour · 43 methods, 6 properties, 40 fields, 1 constructor, 4 events
- `TraderSession` — class, nested, class with behaviour · 3 methods, 12 fields

## Uses
- [[Models/BotAccountOptions.cs.md|BotAccountOptions.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/SimulationFinancialsModels.cs.md|SimulationFinancialsModels.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Bots/IBotService.cs.md|IBotService.cs]]
- [[Services/Simulation/ISimulationEngine.cs.md|ISimulationEngine.cs]]
- [[Services/Simulation/PrefetchingTickReader.cs.md|PrefetchingTickReader.cs]]
- [[Services/Simulation/RingBuffer.cs.md|RingBuffer.cs]]
- [[Services/Simulation/SimulationMarketFeed.cs.md|SimulationMarketFeed.cs]]
- [[Services/Simulation/WatchedSymbolFeed.cs.md|WatchedSymbolFeed.cs]]
- [[Services/Trader/Risk/IRiskInspectable.cs.md|IRiskInspectable.cs]]
- [[Services/Trader/Risk/IRiskPolicyService.cs.md|IRiskPolicyService.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Risk/RiskPolicyModels.cs.md|RiskPolicyModels.cs]]
- [[Services/Trader/Risk/RiskPolicyToggles.cs.md|RiskPolicyToggles.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
