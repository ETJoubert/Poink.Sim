---
tags: [component, feature/trademap, kind/component]
role: component
feature: TradeMap
feature_source: folder
kind: component
code_lines: 32
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/TradeMap/Components/TradeMapCanvas/TradeMapCanvas.razor
---
# TradeMapCanvas.razor

Routes connectors between the hand-placed node cards the same way /code's CodeFlowCanvas routes its auto-laid-out ones, with one addition: a port's side depends on its signal kind, not just its direction.

**Feature:** TradeMap · **Kind:** Razor component · **Code lines:** 32 · **Used by:** 3

## Types
- `TradeMapCanvas` — component, Razor component · empty

## Uses
- [[Components/Pages/TradeMap/Components/TradeMapNodeCard/TradeMapNodeCard.razor.md|TradeMapNodeCard.razor]]
