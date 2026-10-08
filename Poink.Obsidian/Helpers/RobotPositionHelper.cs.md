---
tags: [helper, feature/shared, kind/mixed]
role: helper
feature: Shared
feature_source: shared
kind: mixed
code_lines: 171
types: 3
methods: 9
fan_in: 2
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Helpers/RobotPositionHelper.cs
---
# RobotPositionHelper.cs

Resolves the robot's price-independent TradeConsoleOptions into a concrete entry at a given price and balance, using the same sizing math as TradeConsole's own preview and keeping the result inside what the symbol allows.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 171 · **Used by:** 2

## Types
- `RobotEntryPlan` — record struct, data type (at most one helper method) · 3 properties
- `RobotMarketLevels` — record struct, data type (at most one helper method) · 5 properties
- `RobotPositionHelper` — class, static helper · 9 methods, 1 field

## Uses
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Components/Shared/TradeConsole/TradeConsoleModels.cs.md|TradeConsoleModels.cs]]
- [[Helpers/SymbolTradingLimits.cs.md|SymbolTradingLimits.cs]]
- [[Models/RobotPositionRules.cs.md|RobotPositionRules.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
