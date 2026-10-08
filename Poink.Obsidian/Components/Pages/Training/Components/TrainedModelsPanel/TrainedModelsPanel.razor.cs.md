---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 98
types: 4
methods: 6
fan_in: 3
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/TrainedModelsPanel/TrainedModelsPanel.razor.cs
---
# TrainedModelsPanel.razor.cs

The Models side panel's UI: a Models tab to save the current model and browse/load/delete saved ones, and a History tab to save, load and delete training results. Clicking a model's card selects it, which filters History to that model until the card is clicked again.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 98 · **Used by:** 3

## Types
- `ModelsPanelTab` — enum, enum · 2 values
- `TrainedModelsPanel` — class, Razor component · 6 methods, 28 properties, 2 fields
- `ModelRow` — record, nested, data type (at most one helper method) · 5 properties
- `HistoryRow` — record, nested, data type (at most one helper method) · 8 properties

## Uses
- [[Components/Pages/Training/Components/TrainedModelsPanel/TrainedModelsPanel.razor.md|TrainedModelsPanel.razor]]
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
