---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/RewardBaseline.cs
---
# RewardBaseline.cs

Exponentially weighted mean/variance of the rewards seen so far in a run. Trade Outcome learning can't use the episode's own mean/std as its baseline because it updates before the episode ends.

**Used by:** 1 file (see Backlinks)

## Uses
- _nothing in Poink.Sim_
