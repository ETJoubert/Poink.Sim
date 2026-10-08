---
tags: [code-behind]
source: Poink.Sim/Components/Pages/Training/Components/TrendLineChart/TrendLineChart.razor.cs
---
# TrendLineChart.razor.cs

Generic rolling-window scatter chart for a per-episode statistic; callers supply only how to reduce a window of episodes to one number (Aggregate). Points only, no connecting line/fill, so trend is read from the point cloud's shape.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Pages/Training/Components/TrendLineChart/TrendLineChart.razor.md|TrendLineChart.razor]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
