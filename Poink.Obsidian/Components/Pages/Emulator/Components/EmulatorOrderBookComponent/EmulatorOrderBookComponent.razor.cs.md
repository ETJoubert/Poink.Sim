---
tags: [code-behind, feature/emulator, kind/component-code]
role: code-behind
feature: Emulator
feature_source: folder
kind: component-code
code_lines: 48
types: 1
methods: 7
fan_in: 0
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Emulator/Components/EmulatorOrderBookComponent/EmulatorOrderBookComponent.razor.cs
---
# EmulatorOrderBookComponent.razor.cs

Price ladder view of an order book on the market scale it shares with the chart, so each row sits level with its price on the chart: bids and asks shaded by their share of the deepest row, with the bots whose positions were entered there and their equity alongside.

**Feature:** Emulator · **Kind:** component code-behind · **Code lines:** 48 · **Used by:** 0

## Types
- `EmulatorOrderBookComponent` — class, Razor component · 7 methods, 4 properties, 3 fields

## Uses
- [[Components/Pages/Emulator/Components/EmulatorOrderBookComponent/EmulatorOrderBookComponent.razor.md|EmulatorOrderBookComponent.razor]]
- [[Helpers/EmulatorChartHelper.cs.md|EmulatorChartHelper.cs]]
- [[Helpers/EmulatorLadderHelper.cs.md|EmulatorLadderHelper.cs]]
- [[Helpers/EmulatorMarketScaleHelper.cs.md|EmulatorMarketScaleHelper.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
