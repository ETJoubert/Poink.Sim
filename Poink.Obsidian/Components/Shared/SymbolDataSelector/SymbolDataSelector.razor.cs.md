---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 126
types: 2
methods: 9
fan_in: 0
fan_out: 8
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/SymbolDataSelector/SymbolDataSelector.razor.cs
---
# SymbolDataSelector.razor.cs

Symbol search + date-range slider for picking historic tick data -- unlike TickDataSource (which lists ticks_&lt;symbol&gt; tables directly), this searches the Symbol catalog and resolves the backing table from the match, so callers select a symbol rather than a table name.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 126 · **Used by:** 0

## Types
- `SymbolDataSelector` — class, Razor component · 9 methods, 16 properties, 8 fields
- `SymbolOption` — record, nested, data type (at most one helper method) · 6 properties

## Uses
- [[Components/Shared/SymbolDataSelector/SymbolDataSelector.razor.md|SymbolDataSelector.razor]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
- [[Services/Symbols/ISymbolService.cs.md|ISymbolService.cs]]
- [[Services/Symbols/SymbolService.cs.md|SymbolService.cs]]
