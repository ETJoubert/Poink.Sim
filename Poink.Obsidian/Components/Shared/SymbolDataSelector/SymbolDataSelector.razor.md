---
tags: [component]
source: Poink.Sim/Components/Shared/SymbolDataSelector/SymbolDataSelector.razor
---
# SymbolDataSelector.razor

Symbol search + date-range slider for picking historic tick data -- unlike TickDataSource (which lists ticks_&lt;symbol&gt; tables directly), this searches the Symbol catalog and resolves the backing table from the match, so callers select a symbol rather than a table name.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Components/Shared/DateRangeSlider/DateRangeSlider.razor.md|DateRangeSlider.razor]]
