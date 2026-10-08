---
tags: [interface]
source: Poink.Sim/Helpers/ISessionStorageHelper.cs
---
# ISessionStorageHelper.cs

Browser sessionStorage access, JSON-serialized per value: kept across reloads of the same tab, dropped when the tab closes. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from OnInitializedAsync).

**Used by:** 3 files (see Backlinks)

## Uses
- _nothing in Poink.Sim_
