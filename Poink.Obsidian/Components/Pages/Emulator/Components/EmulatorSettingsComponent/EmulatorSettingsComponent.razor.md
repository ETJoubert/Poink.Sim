---
tags: [component, feature/emulator, kind/component]
role: component
feature: Emulator
feature_source: folder
kind: component
code_lines: 43
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Emulator/Components/EmulatorSettingsComponent/EmulatorSettingsComponent.razor
---
# EmulatorSettingsComponent.razor

Settings column for the emulator: the broker's terms and how bots trade, all applied to the running market. Edits a draft and hands it back on Apply; while the draft is untouched it follows the settings in use. The bot groups are edited in the Bots column.

**Feature:** Emulator · **Kind:** Razor component · **Code lines:** 43 · **Used by:** 3

## Types
- `EmulatorSettingsComponent` — component, Razor component · empty

## Uses
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
