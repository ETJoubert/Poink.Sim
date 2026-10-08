---
tags: [helper]
source: Poink.Sim/Components/Pages/Trade/Helpers/SymbolCoverageHelper.cs
---
# SymbolCoverageHelper.cs

Maps cTrader symbols to their stored tick history. Coverage is judged on the live table because Trade page history before today always reads it; "current" means it reaches the last weekday before today, the newest day the Data page import can supply.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Components/Pages/Trade/Models/SymbolCoverageModels.cs.md|SymbolCoverageModels.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
