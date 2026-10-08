---
tags: [settings]
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridDisplaySettings.cs
---
# XGridDisplaySettings.cs

The row/column overlay toggles XGridController owns and passes into XGrid for both editing and rendering -- immutable so a change always flows back through XGrid's DisplayChanged callback rather than being mutated in place.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Components/Charts/XGrid/Helper/XGridDwellComparison.cs.md|XGridDwellComparison.cs]]
- [[Components/Charts/XGrid/Helper/XGridSweepMode.cs.md|XGridSweepMode.cs]]
