---
tags: [helper, feature/trade, kind/data, candidate/move-to-feature]
role: helper
feature: Trade
feature_source: usage
kind: data
code_lines: 6
types: 1
methods: 0
fan_in: 1
fan_out: 2
summary_source: interface-doc
candidates: [move-to-feature]
source: Poink.Sim/Helpers/SessionStorageHelper.cs
---
# SessionStorageHelper.cs

Browser sessionStorage access, JSON-serialized per value: kept across reloads of the same tab, dropped when the tab closes. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Feature:** Trade (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 6 · **Used by:** 1

## Types
- `SessionStorageHelper` — class, data type (at most one helper method) · 1 constructor

## Candidates
- **move-to-feature** — Used only by the Trade feature; move it into that feature's folder.

## Uses
- [[Helpers/BrowserStorageHelper.cs.md|BrowserStorageHelper.cs]]
- [[Helpers/ISessionStorageHelper.cs.md|ISessionStorageHelper.cs]]
