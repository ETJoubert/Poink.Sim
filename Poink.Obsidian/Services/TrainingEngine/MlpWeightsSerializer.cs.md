---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/MlpWeightsSerializer.cs
---
# MlpWeightsSerializer.cs

Converts an MlpNetwork to and from bytes for storage: a format version, the layer sizes, then each layer's weights (row-major) followed by its biases, all little-endian.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
