---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/DataProvider.cs
---
# DataProvider.cs

Supplies one episode's worth of tick data for training. EpisodeSamplingMode picks how: the entire selected range, an explicit Lookback Window, or (the default) a random window sized/positioned from EpisodeTimingOptionsModel, honoring its weekday/session restriction.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Models/TrainingModels/DataOptionsModel.cs.md|DataOptionsModel.cs]]
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/TrainingEngine/EpisodeData.cs.md|EpisodeData.cs]]
