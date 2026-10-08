---
tags: [service]
source: Poink.Sim/Services/Analysis/BarrierMapAnalysisService.cs
---
# BarrierMapAnalysisService.cs

Runs barrier maps in the background: a trade every few seconds through a range, tested against a grid of stop-loss and take-profit sizes and time limits. One run at a time.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Models/BarrierMapModels.cs.md|BarrierMapModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Analysis/BarrierMapCalculator.cs.md|BarrierMapCalculator.cs]]
- [[Services/Analysis/Interfaces/IBarrierMapAnalysisService.cs.md|IBarrierMapAnalysisService.cs]]
