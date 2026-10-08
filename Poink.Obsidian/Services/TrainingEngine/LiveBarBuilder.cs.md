---
tags: [class, feature/trainingengine, kind/single-method, candidate/single-method]
role: class
feature: TrainingEngine
feature_source: folder
kind: single-method
code_lines: 22
types: 1
methods: 1
fan_in: 1
fan_out: 2
summary_source: documented
candidates: [single-method]
source: Poink.Sim/Services/TrainingEngine/LiveBarBuilder.cs
---
# LiveBarBuilder.cs

Incrementally folds a tick stream into fixed-width OHLC bars, mid-price (Bid+Ask)/2 per tick -- feeds the Live tab's chart from an episode's ticks as they're processed, one at a time, without re-aggregating history on every call.

**Feature:** TrainingEngine · **Kind:** single-method class · **Code lines:** 22 · **Used by:** 1

## Types
- `LiveBarBuilder` — class, single-method class · 1 method, 3 fields

## Candidates
- **single-method** — Inline `Feed` into its only user `TrainingEngineService.cs`.

## Uses
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
