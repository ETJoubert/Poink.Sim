---
tags: [code-behind, feature/ruleengine, kind/component-code]
role: code-behind
feature: RuleEngine
feature_source: folder
kind: component-code
code_lines: 138
types: 1
methods: 14
fan_in: 0
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/RuleEngine/RuleEnginePanel.razor.cs
---
# RuleEnginePanel.razor.cs

Above/In/Below mask over the rule-engine signals (baseline POC/VAH/VAL/VWAP/Median/Regression, XGrid's rank/delta rows, and TxFold's per-timeframe mean+/-stddev rows) compared against their live state, with a per-row Match dot and a match log.

**Feature:** RuleEngine · **Kind:** component code-behind · **Code lines:** 138 · **Used by:** 0

## Types
- `RuleEnginePanel` — class, Razor component · 14 methods, 24 properties, 7 fields

## Uses
- [[Components/RuleEngine/RuleEnginePanel.razor.md|RuleEnginePanel.razor]]
- [[Helpers/RuleEngineHelper.cs.md|RuleEngineHelper.cs]]
- [[Helpers/TxFoldBarsCache.cs.md|TxFoldBarsCache.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
