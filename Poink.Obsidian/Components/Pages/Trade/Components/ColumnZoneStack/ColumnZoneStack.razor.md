---
tags: [component, feature/trade, kind/component]
role: component
feature: Trade
feature_source: folder
kind: component
code_lines: 69
types: 1
methods: 0
fan_in: 3
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/ColumnZoneStack/ColumnZoneStack.razor
---
# ColumnZoneStack.razor

One ColumnZoneSlider per XGrid column from the latest (n) back to n-k, plus the zone gate's controls. The settings are bound by the owner, which applies the gate; this component edits them and keeps them per browser.

**Feature:** Trade · **Kind:** Razor component · **Code lines:** 69 · **Used by:** 3

## Types
- `ColumnZoneStack` — component, Razor component · empty

## Uses
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Pages/Emulator/Components/EmulatorStepperComponent/EmulatorStepperComponent.razor.md|EmulatorStepperComponent.razor]]
- [[Components/Pages/Trade/Components/ColumnZoneSlider/ColumnZoneSlider.razor.md|ColumnZoneSlider.razor]]
- [[Components/Pages/Trade/Helpers/ColumnZoneHelper.cs.md|ColumnZoneHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
