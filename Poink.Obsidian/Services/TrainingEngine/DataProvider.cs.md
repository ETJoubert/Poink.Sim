---
tags: [class, feature/trainingengine, kind/behavior]
role: class
feature: TrainingEngine
feature_source: folder
kind: behavior
code_lines: 97
types: 1
methods: 6
fan_in: 2
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/DataProvider.cs
---
# DataProvider.cs

Supplies one episode's worth of tick data for training. EpisodeSamplingMode picks how: the entire selected range, an explicit Lookback Window, or (the default) a random window sized/positioned from EpisodeTimingOptionsModel, honoring its weekday/session restriction.

**Feature:** TrainingEngine · **Kind:** class with behaviour · **Code lines:** 97 · **Used by:** 2

## Types
- `DataProvider` — class, class with behaviour · 6 methods, 3 fields, 1 constructor

## Uses
- [[Models/TrainingModels/DataOptionsModel.cs.md|DataOptionsModel.cs]]
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/TrainingEngine/EpisodeData.cs.md|EpisodeData.cs]]
