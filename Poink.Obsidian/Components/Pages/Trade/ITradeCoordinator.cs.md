---
tags: [interface, feature/trade, kind/interface]
role: interface
feature: Trade
feature_source: folder
kind: interface
code_lines: 18
types: 1
methods: 6
fan_in: 3
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/ITradeCoordinator.cs
---
# ITradeCoordinator.cs

The Trade page's cross-panel UI state -- which symbol's chart is open, both side panels' collapsed state, and settings persistence -- none of which any driver owns itself.

**Feature:** Trade · **Kind:** interface · **Code lines:** 18 · **Used by:** 3

## Types
- `ITradeCoordinator` — interface, interface · 6 methods, 7 properties, 1 event

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
