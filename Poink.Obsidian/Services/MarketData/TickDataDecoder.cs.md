---
tags: [class]
source: Poink.Sim/Services/MarketData/TickDataDecoder.cs
---
# TickDataDecoder.cs

Decodes cTrader ProtoOAGetTickData responses (newest-first, delta-encoded: the first element is absolute, each subsequent element is the signed difference from the prior one) and merges a bid and an ask series into mid-quote ticks.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
