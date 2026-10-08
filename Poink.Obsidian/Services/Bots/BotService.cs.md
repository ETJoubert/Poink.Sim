---
tags: [service, feature/bots, kind/behavior]
role: service
feature: Bots
feature_source: folder
kind: behavior
code_lines: 235
types: 1
methods: 13
fan_in: 1
fan_out: 22
summary_source: documented
candidates: []
source: Poink.Sim/Services/Bots/BotService.cs
---
# BotService.cs

A flat bot opens long (else short) when that side's conditions all match; a bot in a position closes when the close conditions match. Orders go through the account's normal path, so risk policies still gate them.

**Feature:** Bots · **Kind:** class with behaviour · **Code lines:** 235 · **Used by:** 1

## Types
- `BotService` — class, class with behaviour · 13 methods, 1 property, 14 fields, 1 constructor

## Uses
- [[Helpers/MarketRegimeHelper.cs.md|MarketRegimeHelper.cs]]
- [[Helpers/RangeStatsHelper.cs.md|RangeStatsHelper.cs]]
- [[Helpers/RobotPositionHelper.cs.md|RobotPositionHelper.cs]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Helpers/RuleSignalRowsHelper.cs.md|RuleSignalRowsHelper.cs]]
- [[Helpers/SymbolTradingLimits.cs.md|SymbolTradingLimits.cs]]
- [[Helpers/TxFoldBarsCache.cs.md|TxFoldBarsCache.cs]]
- [[Models/BotAccountOptions.cs.md|BotAccountOptions.cs]]
- [[Models/RobotOptions.cs.md|RobotOptions.cs]]
- [[Models/RobotPositionRules.cs.md|RobotPositionRules.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Bots/BotModels.cs.md|BotModels.cs]]
- [[Services/Bots/BotReentryGuard.cs.md|BotReentryGuard.cs]]
- [[Services/Bots/IBotService.cs.md|IBotService.cs]]
- [[Services/Simulation/RobotRuleContext.cs.md|RobotRuleContext.cs]]
- [[Services/Trader/Commission/PercentOfNotionalCommission.cs.md|PercentOfNotionalCommission.cs]]
- [[Services/Trader/ITraderFactory.cs.md|ITraderFactory.cs]]
- [[Services/Trader/Risk/RiskPolicyToggles.cs.md|RiskPolicyToggles.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
