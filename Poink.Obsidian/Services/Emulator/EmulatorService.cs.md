---
tags: [service]
source: Poink.Sim/Services/Emulator/EmulatorService.cs
---
# EmulatorService.cs

Process-wide emulated brokerage for one synthetic asset. Each step, bots in random order each get a chance to act on what they alone can see; the broker fills their orders, which moves the price, then stops out under-margined bots.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/Emulator/Bots/IEmulatorBotStrategy.cs.md|IEmulatorBotStrategy.cs]]
- [[Services/Emulator/Broker/EmulatorBroker.cs.md|EmulatorBroker.cs]]
- [[Services/Emulator/Broker/EmulatorBrokerModels.cs.md|EmulatorBrokerModels.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/IEmulatorService.cs.md|IEmulatorService.cs]]
