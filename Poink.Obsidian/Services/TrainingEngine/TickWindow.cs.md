---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 27
types: 1
methods: 3
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/TickWindow.cs
---
# TickWindow.cs

A no-copy view over a slice of a tick list. The episode loop grows the view's end one tick at a time so features only ever see ticks up to the current one, and feature math narrows the start to the trailing window. Views over views are flattened onto the underlying list.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 27 · **Used by:** 3

## Types
- `TickWindow` — class, class with behaviour · 3 methods, 2 properties, 3 fields, 1 constructor

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
