---
tags: [settings, feature/emulator, kind/behavior, candidate/move-to-feature]
role: settings
feature: Emulator
feature_source: usage
kind: behavior
code_lines: 45
types: 1
methods: 2
fan_in: 1
fan_out: 2
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/EmulatorPageSettings.cs
---
# EmulatorPageSettings.cs

What the Emulator page saves in the browser: the choices the user made (populations and their starting capital, leverage, broker fund, order range, action frequency range, playback speed, chart view), never internal timings, so later default changes still take effect.

**Feature:** Emulator (by usage) · **Kind:** class with behaviour · **Code lines:** 45 · **Used by:** 1

## Types
- `EmulatorPageSettings` — record, class with behaviour · 2 methods, 8 properties, 1 field

## Candidates
- **move-to-feature** — Used only by the Emulator feature; move it into that feature's folder.

## Uses
- [[Models/EmulatorChartMode.cs.md|EmulatorChartMode.cs]]
- [[Services/Emulator/EmulatorModels.cs.md|EmulatorModels.cs]]
