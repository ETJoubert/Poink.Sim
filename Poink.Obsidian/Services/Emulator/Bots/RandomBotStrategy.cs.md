---
tags: [class]
source: Poink.Sim/Services/Emulator/Bots/RandomBotStrategy.cs
---
# RandomBotStrategy.cs

Zero-intelligence trader: sometimes cancels one of its resting orders at random; otherwise rolls its group's chances to buy, sell or close its position (doing nothing for the rest). A buy or sell goes at a random level within OrderRangeTicks of the last price for a random volume its margin allows.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/Emulator/Bots/EmulatorBotModels.cs.md|EmulatorBotModels.cs]]
- [[Services/Emulator/Bots/IEmulatorBotStrategy.cs.md|IEmulatorBotStrategy.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
