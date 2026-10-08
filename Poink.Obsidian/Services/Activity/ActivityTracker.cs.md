---
tags: [class]
source: Poink.Sim/Services/Activity/ActivityTracker.cs
---
# ActivityTracker.cs

Watches every registered IActivitySource, turning running-to-idle transitions into completions and holding the latest one per route until Acknowledge is called for it.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Services/Activity/ActivityModels.cs.md|ActivityModels.cs]]
- [[Services/Activity/IActivitySource.cs.md|IActivitySource.cs]]
- [[Services/Activity/IActivityTracker.cs.md|IActivityTracker.cs]]
