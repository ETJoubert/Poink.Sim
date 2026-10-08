---
tags: [code-behind]
source: Poink.Sim/Components/Shared/SymbolDataSelector/SymbolDataSelector.razor.cs
---
# SymbolDataSelector.razor.cs

Symbol search + date-range slider for picking historic tick data -- unlike TickDataSource (which lists ticks_&lt;symbol&gt; tables directly), this searches the Symbol catalog and resolves the backing table from the match, so callers select a symbol rather than a table name.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Shared/SymbolDataSelector/SymbolDataSelector.razor.md|SymbolDataSelector.razor]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
- [[Services/Symbols/ISymbolService.cs.md|ISymbolService.cs]]
- [[Services/Symbols/SymbolService.cs.md|SymbolService.cs]]
