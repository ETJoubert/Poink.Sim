---
tags: [code-behind, feature/emulator, kind/component-code]
role: code-behind
feature: Emulator
feature_source: folder
kind: component-code
code_lines: 92
types: 1
methods: 15
fan_in: 0
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Emulator/EmulatorPage.razor.cs
---
# EmulatorPage.razor.cs

Brokerage emulator: a synthetic asset's price, order book, bots and broker, driven by IEmulatorService. The user's settings and playback speed are saved in the browser and restored when the page first renders.

**Feature:** Emulator · **Kind:** component code-behind · **Code lines:** 92 · **Used by:** 0

## Types
- `EmulatorPage` — class, Razor component · 15 methods, 5 properties, 3 fields

## Uses
- [[Components/Pages/Emulator/EmulatorPage.razor.md|EmulatorPage.razor]]
- [[Helpers/EmulatorChartHelper.cs.md|EmulatorChartHelper.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/EmulatorChartMode.cs.md|EmulatorChartMode.cs]]
- [[Models/EmulatorPageSettings.cs.md|EmulatorPageSettings.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
- [[Services/Emulator/IEmulatorService.cs.md|IEmulatorService.cs]]
