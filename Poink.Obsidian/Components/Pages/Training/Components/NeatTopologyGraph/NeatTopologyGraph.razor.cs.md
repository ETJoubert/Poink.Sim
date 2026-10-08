---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 159
types: 1
methods: 7
fan_in: 0
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/NeatTopologyGraph/NeatTopologyGraph.razor.cs
---
# NeatTopologyGraph.razor.cs

Live render of one NEAT genome's graph: Input/Bias anchor left, Output anchors right, and Hidden nodes flow between them by topological depth. Drawn on &lt;canvas&gt; via JS interop, same pattern as XGrid/MarketChart/CandlestickChart.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 159 · **Used by:** 0

## Types
- `NeatTopologyGraph` — class, Razor component · 7 methods, 7 properties, 15 fields

## Uses
- [[Components/Pages/Training/Components/NeatTopologyGraph/NeatTopologyGraph.razor.md|NeatTopologyGraph.razor]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
- [[Services/Brain/Neural/Neat/NeatModels.cs.md|NeatModels.cs]]
- [[Services/Brain/Neural/Neat/NeatTopologyDescriber.cs.md|NeatTopologyDescriber.cs]]
- [[wwwroot/js/neatTopologyGraph.js.md|neatTopologyGraph.js]]
