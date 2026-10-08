---
tags: [code-behind, feature/emulator, kind/component-code]
role: code-behind
feature: Emulator
feature_source: folder
kind: component-code
code_lines: 100
types: 2
methods: 5
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Emulator/Components/EmulatorGroupCardComponent/EmulatorGroupCardComponent.razor.cs
---
# EmulatorGroupCardComponent.razor.cs

One bot group as a card: its bots' accounts on the left and its settings on the right. A valid edit is handed back as soon as a field changes; an invalid one stays on the card with the reason until it is fixed.

**Feature:** Emulator · **Kind:** component code-behind · **Code lines:** 100 · **Used by:** 0

## Types
- `EmulatorGroupCardComponent` — class, Razor component · 5 methods, 14 properties, 20 fields, 1 constructor
- `WeightField` — record, nested, data type (at most one helper method) · 4 properties

## Uses
- [[Components/Pages/Emulator/Components/EmulatorGroupCardComponent/EmulatorGroupCardComponent.razor.md|EmulatorGroupCardComponent.razor]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
