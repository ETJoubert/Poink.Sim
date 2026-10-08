---
tags: [page, feature/emulator, kind/page]
role: page
feature: Emulator
feature_source: folder
kind: page
code_lines: 96
types: 1
methods: 0
fan_in: 2
fan_out: 8
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Emulator/EmulatorPage.razor
---
# EmulatorPage.razor

Brokerage emulator: a synthetic asset's price, order book, bots and broker, driven by IEmulatorService. The user's settings and playback speed are saved in the browser and restored when the page first renders.

**Feature:** Emulator · **Kind:** Razor page · **Code lines:** 96 · **Used by:** 2

## Types
- `EmulatorPage` — component, Razor component · empty

## Uses
- [[Components/Pages/Emulator/Components/EmulatorBrokerComponent/EmulatorBrokerComponent.razor.md|EmulatorBrokerComponent.razor]]
- [[Components/Pages/Emulator/Components/EmulatorEquityCurveComponent/EmulatorEquityCurveComponent.razor.md|EmulatorEquityCurveComponent.razor]]
- [[Components/Pages/Emulator/Components/EmulatorGroupCardComponent/EmulatorGroupCardComponent.razor.md|EmulatorGroupCardComponent.razor]]
- [[Components/Pages/Emulator/Components/EmulatorMarketComponent/EmulatorMarketComponent.razor.md|EmulatorMarketComponent.razor]]
- [[Components/Pages/Emulator/Components/EmulatorPopulationsComponent/EmulatorPopulationsComponent.razor.md|EmulatorPopulationsComponent.razor]]
- [[Components/Pages/Emulator/Components/EmulatorSettingsComponent/EmulatorSettingsComponent.razor.md|EmulatorSettingsComponent.razor]]
- [[Helpers/EmulatorGroupColorHelper.cs.md|EmulatorGroupColorHelper.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
