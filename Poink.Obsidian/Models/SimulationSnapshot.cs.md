---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 91
types: 7
methods: 0
fan_in: 21
fan_out: 9
summary_source: generated
candidates: []
source: Poink.Sim/Models/SimulationSnapshot.cs
---
# SimulationSnapshot.cs

Models, records and DTOs for simulation snapshot.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 91 · **Used by:** 21

## Types
- `SimulationSteppingMode` — enum, enum · 2 values
- `SimulationConfig` — record, data type (at most one helper method) · 16 properties
- `SimulationLogEntry` — record, data type (at most one helper method) · 8 properties
- `SimulationProgress` — record, data type (at most one helper method) · 5 properties
- `TradeEquityPoint` — record struct, data type (at most one helper method) · 5 properties
- `TraderSnapshot` — record, data type (at most one helper method) · 15 properties
- `SimulationSnapshot` — record, data type (at most one helper method) · 19 properties

## Uses
- [[Helpers/SymbolTradingLimits.cs.md|SymbolTradingLimits.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationWatchModels.cs.md|SimulationWatchModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Trader/Orders/PendingOrder.cs.md|PendingOrder.cs]]
- [[Services/Trader/Risk/RiskPolicyModels.cs.md|RiskPolicyModels.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
