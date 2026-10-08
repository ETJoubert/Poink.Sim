---
tags: [model]
source: Poink.Sim/Services/Emulator/Broker/EmulatorBrokerModels.cs
---
# EmulatorBrokerModels.cs

One bot's account at the broker. Negative cash is points the broker lent for a leveraged buy; a negative position is units it lent for a short sale. Capital is every point paid in (starting capital plus replenishments); equity is cash plus the open position at the given price.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
