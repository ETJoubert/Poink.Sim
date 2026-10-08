---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/AdamOptimizer.cs
---
# AdamOptimizer.cs

Adam weight updates for one network: per-weight running averages of the gradient and its square scale each step, so the learning rate means the same thing whatever the gradient's size. State is per instance, so each network being trained needs its own.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
