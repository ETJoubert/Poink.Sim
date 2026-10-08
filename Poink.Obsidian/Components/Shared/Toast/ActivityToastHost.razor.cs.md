---
tags: [code-behind, feature/shared, kind/component-code]
role: code-behind
feature: Shared
feature_source: shared
kind: component-code
code_lines: 40
types: 2
methods: 5
fan_in: 0
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/Toast/ActivityToastHost.razor.cs
---
# ActivityToastHost.razor.cs

Pops a toast for every background run that completes or fails, linking back to the tab it ran on.

**Feature:** Shared · **Kind:** component code-behind · **Code lines:** 40 · **Used by:** 0

## Types
- `ActivityToastHost` — class, Razor component · 5 methods, 2 properties, 5 fields
- `ActivityToast` — record, nested, data type (at most one helper method) · 2 properties

## Uses
- [[Components/Shared/Toast/ActivityToastHost.razor.md|ActivityToastHost.razor]]
- [[Services/Activity/ActivityModels.cs.md|ActivityModels.cs]]
- [[Services/Activity/ActivityTracker.cs.md|ActivityTracker.cs]]
- [[Services/Activity/IActivityTracker.cs.md|IActivityTracker.cs]]
