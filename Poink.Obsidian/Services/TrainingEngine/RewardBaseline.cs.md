---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 21
types: 1
methods: 2
fan_in: 1
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/RewardBaseline.cs
---
# RewardBaseline.cs

Exponentially weighted mean/variance of the rewards seen so far in a run. Trade Outcome learning can't use the episode's own mean/std as its baseline because it updates before the episode ends.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 21 · **Used by:** 1

## Types
- `RewardBaseline` — class, class with behaviour · 2 methods, 5 fields

## Uses
- _nothing in Poink.Sim_
