---
tags: [model, feature/training, kind/data-bundle]
role: model
feature: Training
feature_source: folder
kind: data-bundle
code_lines: 39
types: 4
methods: 1
fan_in: 5
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Models/TrainingModels/PositionControlModels.cs
---
# PositionControlModels.cs

The trade parameters the trader is allowed to use. Volume is in units of the instrument. StopLoss is a distance in StopLossUnit (0 = no stop); RiskPerTradePercent and TakeProfitBalancePercent are percents of balance (0 take-profit = no target).

**Feature:** Training · **Kind:** bundle of data types · **Code lines:** 39 · **Used by:** 5

## Types
- `PositionSizingMode` — enum, enum · 2 values
- `StopLossUnit` — enum, enum · 2 values
- `PositionControlOptionsModel` — record, data type (at most one helper method) · 1 method, 9 properties
- `PositionSizingPreview` — record struct, data type (at most one helper method) · 9 properties

## Uses
- [[Components/Shared/TradeConsole/TradeConsoleModels.cs.md|TradeConsoleModels.cs]]
