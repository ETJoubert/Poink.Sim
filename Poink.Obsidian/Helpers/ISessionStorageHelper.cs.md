---
tags: [interface, feature/trade, kind/interface, candidate/move-to-feature]
role: interface
feature: Trade
feature_source: usage
kind: interface
code_lines: 5
types: 1
methods: 2
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Helpers/ISessionStorageHelper.cs
---
# ISessionStorageHelper.cs

Browser sessionStorage access, JSON-serialized per value: kept across reloads of the same tab, dropped when the tab closes. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Feature:** Trade (by usage) · **Kind:** interface · **Code lines:** 5 · **Used by:** 3

## Types
- `ISessionStorageHelper` — interface, interface · 2 methods

## Candidates
- **move-to-feature** — Used only by the Trade feature; move it into that feature's folder.

## Uses
- _nothing in Poink.Sim_
