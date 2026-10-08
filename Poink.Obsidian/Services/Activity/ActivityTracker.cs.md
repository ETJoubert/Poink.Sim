---
tags: [class, feature/activity, kind/behavior]
role: class
feature: Activity
feature_source: folder
kind: behavior
code_lines: 74
types: 1
methods: 6
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/Activity/ActivityTracker.cs
---
# ActivityTracker.cs

Watches every registered IActivitySource, turning running-to-idle transitions into completions and holding the latest one per route until Acknowledge is called for it.

**Feature:** Activity · **Kind:** class with behaviour · **Code lines:** 74 · **Used by:** 3

## Types
- `ActivityTracker` — class, class with behaviour · 6 methods, 6 fields, 1 constructor, 2 events

## Uses
- [[Services/Activity/ActivityModels.cs.md|ActivityModels.cs]]
- [[Services/Activity/IActivitySource.cs.md|IActivitySource.cs]]
- [[Services/Activity/IActivityTracker.cs.md|IActivityTracker.cs]]
