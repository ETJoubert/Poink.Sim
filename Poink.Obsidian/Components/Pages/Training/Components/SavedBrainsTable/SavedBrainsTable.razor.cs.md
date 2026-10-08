---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 55
types: 2
methods: 2
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/SavedBrainsTable/SavedBrainsTable.razor.cs
---
# SavedBrainsTable.razor.cs

Browsable history of saved brain snapshots, shared between TrainPage (continue training from a prior snapshot) and SimulationPage (pick a trained brain to run).

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 55 · **Used by:** 1

## Types
- `SavedBrainsTable` — class, Razor component · 2 methods, 7 properties, 1 field
- `SavedBrainRow` — record, nested, data type (at most one helper method) · 12 properties

## Uses
- [[Components/Pages/Training/Components/SavedBrainsTable/SavedBrainsTable.razor.md|SavedBrainsTable.razor]]
- [[Services/Training/ModelService.cs.md|ModelService.cs]]
