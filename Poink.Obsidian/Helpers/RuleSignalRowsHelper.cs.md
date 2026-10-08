---
tags: [helper, feature/shared, kind/mixed]
role: helper
feature: Shared
feature_source: shared
kind: mixed
code_lines: 347
types: 3
methods: 16
fan_in: 5
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Helpers/RuleSignalRowsHelper.cs
---
# RuleSignalRowsHelper.cs

Builds the event, zone, regime, session and position signal rows. Every row keeps the Above/In/Below shape, so masks, Need-N matching and Copy Live work on them unchanged.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 347 · **Used by:** 5

## Types
- `RuleSignalMarket` — record, data type (at most one helper method) · 8 properties
- `RuleSignalLevels` — record struct, data type (at most one helper method) · 6 properties
- `RuleSignalRowsHelper` — class, static helper · 16 methods, 5 fields

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Helpers/MarketRegimeHelper.cs.md|MarketRegimeHelper.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/RuleSignalSettings.cs.md|RuleSignalSettings.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
