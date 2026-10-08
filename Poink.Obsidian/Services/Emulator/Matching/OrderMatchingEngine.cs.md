---
tags: [class]
source: Poink.Sim/Services/Emulator/Matching/OrderMatchingEngine.cs
---
# OrderMatchingEngine.cs

Price-time priority limit order book on a whole-tick grid. An incoming order fills against the best opposite levels at their resting prices, oldest first, and any remainder rests. An incoming order never fills against its own bot: the bot's resting order is cancelled instead, so the book can never cross.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/Matching/MatchingModels.cs.md|MatchingModels.cs]]
