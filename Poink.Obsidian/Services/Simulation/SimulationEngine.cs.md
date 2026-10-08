---
tags: [class]
source: Poink.Sim/Services/Simulation/SimulationEngine.cs
---
# SimulationEngine.cs

Interactive replay driver for one shared, in-process simulation run: plays, pauses and steps ticks through the shared aggregator and trader, and publishes an immutable snapshot after each callback for the UI to render.

**Used by:** 2 files (see Backlinks)

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
