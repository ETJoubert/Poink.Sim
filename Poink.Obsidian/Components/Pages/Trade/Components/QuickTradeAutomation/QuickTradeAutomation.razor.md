---
tags: [component, feature/trade, kind/component]
role: component
feature: Trade
feature_source: folder
kind: component
code_lines: 31
types: 1
methods: 0
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/QuickTradeAutomation/QuickTradeAutomation.razor
---
# QuickTradeAutomation.razor

The quick console's automation toggles: auto-arm after t, a market time stop after t + s, and auto-fire on a zone gate pass. t, s and the two close toggles are kept per browser; auto-fire always starts off, like ARMED.

**Feature:** Trade · **Kind:** Razor component · **Code lines:** 31 · **Used by:** 3

## Types
- `QuickTradeAutomation` — component, Razor component · empty

## Uses
- [[Components/Pages/Emulator/Components/EmulatorStepperComponent/EmulatorStepperComponent.razor.md|EmulatorStepperComponent.razor]]
- [[Components/Pages/Trade/Helpers/PositionCloseHelper.cs.md|PositionCloseHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
