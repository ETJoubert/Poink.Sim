---
tags: [helper]
source: Poink.Sim/Helpers/SplitTickReadHelper.cs
---
# SplitTickReadHelper.cs

Reads one tick range that may span two tables: [from, historyBeforeUtc) from a history table and the rest from the main table. The Trade page needs this on a demo account, whose history lives in the live table while today's ticks are recorded in the demo one.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
