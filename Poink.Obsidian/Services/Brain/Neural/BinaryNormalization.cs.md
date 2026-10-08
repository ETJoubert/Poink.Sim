---
tags: [class, feature/brain, kind/single-method, candidate/single-method]
role: class
feature: Brain
feature_source: folder
kind: single-method
code_lines: 8
types: 1
methods: 1
fan_in: 3
fan_out: 0
summary_source: generated
candidates: [single-method]
source: Poink.Sim/Services/Brain/Neural/BinaryNormalization.cs
---
# BinaryNormalization.cs

Defines binary normalization.

**Feature:** Brain · **Kind:** single-method class · **Code lines:** 8 · **Used by:** 3

## Types
- `BinaryNormalization` — class, single-method class · 1 method

## Candidates
- **single-method** — Fold `Compute` into a sibling helper (`CategoricalSampling.cs`, `ZScoreNormalization.cs`).

## Uses
- _nothing in Poink.Sim_
