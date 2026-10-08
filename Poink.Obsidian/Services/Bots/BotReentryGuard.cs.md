---
tags: [class]
source: Poink.Sim/Services/Bots/BotReentryGuard.cs
---
# BotReentryGuard.cs

Holds each bot back from opening again right after a close: for a cooldown of simulated seconds from the close, and, when re-arm is required, until that side's open conditions have stopped matching at least once.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
