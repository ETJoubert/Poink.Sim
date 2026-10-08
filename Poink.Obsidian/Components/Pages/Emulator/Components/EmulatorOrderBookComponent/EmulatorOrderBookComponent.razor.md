---
tags: [component, feature/emulator, kind/component]
role: component
feature: Emulator
feature_source: folder
kind: component
code_lines: 34
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Emulator/Components/EmulatorOrderBookComponent/EmulatorOrderBookComponent.razor
---
# EmulatorOrderBookComponent.razor

Price ladder view of an order book on the market scale it shares with the chart, so each row sits level with its price on the chart: bids and asks shaded by their share of the deepest row, with the bots whose positions were entered there and their equity alongside.

**Feature:** Emulator · **Kind:** Razor component · **Code lines:** 34 · **Used by:** 3

## Types
- `EmulatorOrderBookComponent` — component, Razor component · empty

## Uses
- [[Helpers/EmulatorChartHelper.cs.md|EmulatorChartHelper.cs]]
