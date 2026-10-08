---
tags: [code-behind, feature/trade, kind/component-code]
role: code-behind
feature: Trade
feature_source: folder
kind: component-code
code_lines: 129
types: 1
methods: 15
fan_in: 0
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/ColumnZoneStack/ColumnZoneStack.razor.cs
---
# ColumnZoneStack.razor.cs

One ColumnZoneSlider per XGrid column from the latest (n) back to n-k, plus the zone gate's controls. The settings are bound by the owner, which applies the gate; this component edits them and keeps them per browser.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 129 · **Used by:** 0

## Types
- `ColumnZoneStack` — class, Razor component · 15 methods, 10 properties, 6 fields

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Trade/Components/ColumnZoneStack/ColumnZoneStack.razor.md|ColumnZoneStack.razor]]
- [[Components/Pages/Trade/Helpers/ColumnZoneHelper.cs.md|ColumnZoneHelper.cs]]
- [[Components/Pages/Trade/Models/ColumnZoneModels.cs.md|ColumnZoneModels.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
