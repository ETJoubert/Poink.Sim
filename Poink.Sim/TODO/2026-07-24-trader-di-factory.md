# Trader construction through DI (ITraderFactory)

Status: Approved

## Why

`ITrader` was extracted (`Trader/ITrader.cs`, `Trader/Trader.cs`) so consumers
(`SimulationEngine.TraderSession`, `RlEpisodeRunner.RecordStep`) reference the
interface instead of the concrete class -- enough to unit-test `Trader`/`ITrader`
in isolation today. Construction still goes through `new Trader.Trader(startingCash,
settings)` directly at both call sites (`SimulationEngine.CreateSessions`,
`RlEpisodeRunner.Run`), which is fine for testing `Trader` itself but doesn't let
a test substitute a fake `ITrader` when testing `SimulationEngine`'s or
`RlEpisodeRunner`'s own orchestration logic (manual-mode gating in
`DecideAndApply`, the `Advance`-then-`Apply` sequencing, `SubmitManualDecision`'s
mode check, etc.) without a real `Trader` underneath. This is the deferred half
of that decoupling -- do it once actual orchestration-level unit tests are being
written and it's clear what they need to mock, not speculatively now.

## Scope

- **`Trader/ITraderFactory.cs`** (new): `internal interface ITraderFactory {
  ITrader Create(double startingCash, TraderSettings? settings = null); }`.
- **`Trader/TraderFactory.cs`** (new): trivial default implementation, `Create`
  just returns `new Trader.Trader(startingCash, settings)`.
- **`Program.cs`**: `builder.Services.AddSingleton<ITraderFactory, TraderFactory>();`
  (stateless, no reason for a shorter lifetime).
- **`SimulationEngine`**: constructor takes `ITraderFactory`; `CreateSessions`
  calls `_traderFactory.Create(startingCash, settings)` instead of `new
  Trader.Trader(...)`.
- **`RlEpisodeRunner.Run`**: currently `static`, takes no injected dependencies
  today (data-source-agnostic by design, see its own doc comment) -- taking an
  `ITraderFactory` parameter here means either threading it through from
  `RlTrainingService` (which *is* DI-constructed, so this is straightforward) or
  reconsidering whether `RlEpisodeRunner` should stay `static` at all. Decide
  this when actually doing the work, not preemptively here.

## Non-goals

- No change to `Trader`'s own constructor or `TraderSettings` shape.
- Not doing this now -- this file exists so the follow-up isn't forgotten, not
  as a green light to start immediately.

## Verification

- `SimulationEngine`/`RlTrainingService` unit tests can construct with a fake
  `ITraderFactory` returning a mock `ITrader`, and assert on calls made to it
  (e.g. `DecideAndApply` skips `Apply` for a Manual-mode session, or
  `SubmitManualDecision` rejects a non-Manual session) without a real `Trader`.
- `dotnet build` clean; existing Simulation/Training behavior unchanged (the
  factory's default `Create` is a one-line passthrough to today's `new
  Trader.Trader(...)`).
