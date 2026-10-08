---
tags: [helper]
source: Poink.Sim/Helpers/SessionStorageHelper.cs
---
# SessionStorageHelper.cs

Browser sessionStorage access, JSON-serialized per value: kept across reloads of the same tab, dropped when the tab closes. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Used by:** 1 file (see Backlinks)

## Uses
- [[Helpers/BrowserStorageHelper.cs.md|BrowserStorageHelper.cs]]
- [[Helpers/ISessionStorageHelper.cs.md|ISessionStorageHelper.cs]]
