---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 205
types: 7
methods: 27
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/RangeCalendar/RangeCalendar.razor.cs
---
# RangeCalendar.razor.cs

One calendar for a start/end datetime pair: the range is shaded across the day grid (and the full-year view), day clicks set the active edge -- moving on to End after Start -- and each edge keeps its own time to the minute.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 205 · **Used by:** 2

## Types
- `RangeEdge` — enum, enum · 2 values
- `RangeCalendar` — class, Razor component · 27 methods, 24 properties, 8 fields
- `CalendarView` — enum, nested, enum · 3 values
- `YearCell` — record, nested, data type (at most one helper method) · 4 properties
- `MonthCell` — record, nested, data type (at most one helper method) · 5 properties
- `DayCell` — record, nested, data type (at most one helper method) · 4 properties
- `MiniMonth` — record, nested, data type (at most one helper method) · 2 properties

## Uses
- [[Components/Shared/RangeCalendar/RangeCalendar.razor.md|RangeCalendar.razor]]
