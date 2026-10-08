---
tags: [helper, feature/shared, kind/data]
role: helper
feature: Shared
feature_source: shared
kind: data
code_lines: 6
types: 1
methods: 0
fan_in: 1
fan_out: 2
summary_source: interface-doc
candidates: []
source: Poink.Sim/Helpers/LocalStorageHelper.cs
---
# LocalStorageHelper.cs

Browser localStorage access, JSON-serialized per value. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 6 · **Used by:** 1

## Types
- `LocalStorageHelper` — class, data type (at most one helper method) · 1 constructor

## Uses
- [[Helpers/BrowserStorageHelper.cs.md|BrowserStorageHelper.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
