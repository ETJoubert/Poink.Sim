---
tags: [page, feature/data, kind/page]
role: page
feature: Data
feature_source: folder
kind: page
code_lines: 410
types: 1
methods: 0
fan_in: 2
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Data/DataPage.razor
---
# DataPage.razor

DaysMissing (in source, no DB row) / DaysMismatched (in both, counts differ -- likely a partial prior import) / DaysMatching (already up to date, safe to skip). NeedsImportDates is Missing union Mismatched -- what a diff-only import writes.

**Feature:** Data · **Kind:** Razor page · **Code lines:** 410 · **Used by:** 2

## Types
- `DataPage` — component, Razor component · empty

## Uses
- [[Components/Charts/DataSeriesSyncChart/DataSeriesSyncChart.razor.md|DataSeriesSyncChart.razor]]
- [[Components/Pages/Data/DataPage.razor.cs.md|DataPage.razor.cs]]
- [[Components/Shared/DataTable/DataTable.razor.md|DataTable.razor]]
- [[Components/Shared/FolderBrowserDialog/FolderBrowserDialog.razor.md|FolderBrowserDialog.razor]]
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
