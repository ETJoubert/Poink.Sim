---
tags: [code-behind, feature/trade, kind/component-code]
role: code-behind
feature: Trade
feature_source: folder
kind: component-code
code_lines: 44
types: 1
methods: 6
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/ColumnZoneSlider/ColumnZoneSlider.razor.cs
---
# ColumnZoneSlider.razor.cs

A horizontal gauge of where the current price sits along one zone axis (a column's price span, or a Time×Tk share), split into a LOW zone on the left, MID, and a HIGH zone on the right; caps either side light when the price is below or above what the axis covers.

**Feature:** Trade · **Kind:** component code-behind · **Code lines:** 44 · **Used by:** 0

## Types
- `ColumnZoneSlider` — class, Razor component · 6 methods, 12 properties

## Uses
- [[Components/Pages/Trade/Components/ColumnZoneSlider/ColumnZoneSlider.razor.md|ColumnZoneSlider.razor]]
- [[Components/Pages/Trade/Helpers/ColumnZoneHelper.cs.md|ColumnZoneHelper.cs]]
- [[Components/Pages/Trade/Models/ColumnZoneModels.cs.md|ColumnZoneModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
