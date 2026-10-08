---
tags: [code-behind, feature/trademap, kind/component-code]
role: code-behind
feature: TradeMap
feature_source: folder
kind: component-code
code_lines: 268
types: 5
methods: 29
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/TradeMap/Components/TradeMapCanvas/TradeMapCanvas.razor.cs
---
# TradeMapCanvas.razor.cs

Routes connectors between the hand-placed node cards the same way /code's CodeFlowCanvas routes its auto-laid-out ones, with one addition: a port's side depends on its signal kind, not just its direction.

**Feature:** TradeMap · **Kind:** component code-behind · **Code lines:** 268 · **Used by:** 0

## Types
- `TradeMapCanvas` — class, Razor component · 29 methods, 13 properties, 16 fields
- `EdgeRoute` — enum, nested, enum · 3 values
- `PortKey` — record struct, nested, data type (at most one helper method) · 3 properties
- `PortAnchor` — record, nested, data type (at most one helper method) · 3 properties
- `EdgeLayout` — record, nested, data type (at most one helper method) · 2 properties

## Uses
- [[Components/Pages/TradeMap/Components/TradeMapCanvas/TradeMapCanvas.razor.md|TradeMapCanvas.razor]]
- [[Components/Pages/TradeMap/Models/TradeMapModels.cs.md|TradeMapModels.cs]]
