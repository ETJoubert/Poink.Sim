---
tags: [class, feature/shared, kind/data]
role: class
feature: Shared
feature_source: shared
kind: data
code_lines: 14
types: 1
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Data/Entities/NeuralNetwork.cs
---
# NeuralNetwork.cs

A network's structure (encoders, architecture, output heads) as built from the frontend. Weights produced by training it are recorded separately in NeuralWeights, since one structure can be retrained many times.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 14 · **Used by:** 3

## Types
- `NeuralNetwork` — class, data type (at most one helper method) · 6 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Data/Entities/NeuralWeights.cs.md|NeuralWeights.cs]]
