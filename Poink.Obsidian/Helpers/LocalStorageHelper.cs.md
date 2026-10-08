---
tags: [helper]
source: Poink.Sim/Helpers/LocalStorageHelper.cs
---
# LocalStorageHelper.cs

Browser localStorage access, JSON-serialized per value. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Used by:** 1 file (see Backlinks)

## Uses
- [[Helpers/BrowserStorageHelper.cs.md|BrowserStorageHelper.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
