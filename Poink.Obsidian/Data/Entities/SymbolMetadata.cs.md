---
tags: [class]
source: Poink.Sim/Data/Entities/SymbolMetadata.cs
---
# SymbolMetadata.cs

Full per-symbol trading detail from cTrader's ProtoOASymbol (digits, volume/commission/swap rules), correlated to Symbol by SymbolId rather than a DB-enforced foreign key, matching how the rest of the app correlates cTrader data by symbol id.

**Used by:** 9 files (see Backlinks)

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Data/Entities/SymbolHoliday.cs.md|SymbolHoliday.cs]]
- [[Data/Entities/SymbolScheduleInterval.cs.md|SymbolScheduleInterval.cs]]
