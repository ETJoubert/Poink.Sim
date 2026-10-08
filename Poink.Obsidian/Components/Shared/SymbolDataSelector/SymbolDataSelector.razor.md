---
tags: [component, feature/shared, kind/component]
role: component
feature: Shared
feature_source: shared
kind: component
code_lines: 39
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/SymbolDataSelector/SymbolDataSelector.razor
---
# SymbolDataSelector.razor

Symbol search + date-range slider for picking historic tick data -- unlike TickDataSource (which lists ticks_&lt;symbol&gt; tables directly), this searches the Symbol catalog and resolves the backing table from the match, so callers select a symbol rather than a table name.

**Feature:** Shared · **Kind:** Razor component · **Code lines:** 39 · **Used by:** 3

## Types
- `SymbolDataSelector` — component, Razor component · empty

## Uses
- [[Components/Shared/DateRangeSlider/DateRangeSlider.razor.md|DateRangeSlider.razor]]
