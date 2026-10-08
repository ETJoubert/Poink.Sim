---
tags: [interface, feature/shared, kind/interface]
role: interface
feature: Shared
feature_source: shared
kind: interface
code_lines: 5
types: 1
methods: 2
fan_in: 13
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Helpers/ILocalStorageHelper.cs
---
# ILocalStorageHelper.cs

Browser localStorage access, JSON-serialized per value. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Feature:** Shared · **Kind:** interface · **Code lines:** 5 · **Used by:** 13

## Types
- `ILocalStorageHelper` — interface, interface · 2 methods

## Uses
- _nothing in Poink.Sim_
