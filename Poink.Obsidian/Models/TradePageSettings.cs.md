---
tags: [settings, feature/trade, kind/data, candidate/move-to-feature]
role: settings
feature: Trade
feature_source: usage
kind: data
code_lines: 26
types: 1
methods: 0
fan_in: 2
fan_out: 4
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/TradePageSettings.cs
---
# TradePageSettings.cs

Snapshot of the Trade page's layout and chart settings, persisted to browser localStorage so it survives navigating away and back. The watchlist is persisted separately, per account -- see TradeCoordinator's account-scoped watchlist storage.

**Feature:** Trade (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 26 · **Used by:** 2

## Types
- `TradePageSettings` — record, data type (at most one helper method) · 20 properties

## Candidates
- **move-to-feature** — Used only by the Trade feature; move it into that feature's folder.

## Uses
- [[Components/Pages/Trade/Models/TradeChartModels.cs.md|TradeChartModels.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
