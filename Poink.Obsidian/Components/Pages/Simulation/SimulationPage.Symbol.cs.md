---
tags: [class, feature/simulation, kind/component-code]
role: class
feature: Simulation
feature_source: folder
kind: component-code
code_lines: 64
types: 1
methods: 3
fan_in: 0
fan_out: 9
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Simulation/SimulationPage.Symbol.cs
---
# SimulationPage.Symbol.cs

Resolves the selected data table to its symbol and trading metadata, so the setup tab can show the symbol's trading parameters and the pip size comes from the symbol instead of being typed in.

**Feature:** Simulation · **Kind:** component code-behind · **Code lines:** 64 · **Used by:** 0

## Types
- `SimulationPage` — class, class with behaviour · 3 methods, 3 properties, 5 fields

## Uses
- [[Components/Pages/Simulation/SimulationPage.razor.md|SimulationPage.razor]]
- [[Helpers/SymbolTradingLimits.cs.md|SymbolTradingLimits.cs]]
- [[Helpers/TickTableRangeHelper.cs.md|TickTableRangeHelper.cs]]
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Services/Symbols/ISymbolTableResolver.cs.md|ISymbolTableResolver.cs]]
- [[Services/Symbols/SymbolTableResolver.cs.md|SymbolTableResolver.cs]]
