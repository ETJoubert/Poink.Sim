---
tags: [component, feature/training, kind/component]
role: component
feature: Training
feature_source: folder
kind: component
code_lines: 48
types: 1
methods: 0
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/TrainedModelsPanel/TrainedModelsPanel.razor
---
# TrainedModelsPanel.razor

The Models side panel's UI: a Models tab to save the current model and browse/load/delete saved ones, and a History tab to save, load and delete training results. Clicking a model's card selects it, which filters History to that model until the card is clicked again.

**Feature:** Training · **Kind:** Razor component · **Code lines:** 48 · **Used by:** 4

## Types
- `TrainedModelsPanel` — component, Razor component · empty

## Uses
- [[Components/Pages/Training/Components/TrainedModelsPanel/TrainedModelsPanel.razor.cs.md|TrainedModelsPanel.razor.cs]]
- [[Components/Shared/DataTable/DataTable.razor.md|DataTable.razor]]
