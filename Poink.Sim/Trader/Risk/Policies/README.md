# Policies

Concrete `IRiskPolicy` rules. See `../README.md` for how the layer is wired.

## Adding a policy

1. Add one class here implementing `IRiskPolicy`.
2. That is all. `AddRiskPolicies()` discovers it by reflection at startup, so it needs no DI registration and appears automatically in the setup checkboxes, the Train page, and the run-time toggle panel.

## The contract

```csharp
string Name { get; }          // selection key and log label; must be unique
string Description { get; }    // shown as the control's tooltip
IRiskPolicy Fresh();           // a fresh per-trader instance (see State)
RiskReview Review(SimulationDecision proposed, RiskContext context);
IReadOnlyList<SimulationDecision> Supervise(RiskContext context);
```

- **Review** gates a proposed action. Return `RiskReview.Permit(proposed)` to allow it, `RiskReview.Deny(proposed, reason)` to block it, or `RiskReview.Modify(changed, reason)` to alter it (resize, attach a stop, ...). Only entries usually need attention; permit `Hold`/`Close` unchanged. A deny is terminal across the policy chain; a modify feeds the next policy.
- **Supervise** runs every tick on the open position and returns zero or more decisions to inject (trail a stop, time-exit, ...). Return `Array.Empty<SimulationDecision>()` for a gate-only policy. Injected actions are executed directly and do not re-enter the gate.

## State

`Fresh()` gives each trader its own instance, so state never bleeds between traders.

- **Stateless** policy: `Fresh()` returns a new instance carrying the same config; hold no mutable fields.
- **Stateful** policy: keep mutable fields and update them in `Supervise` (it is the only hook that runs every tick and sees the closed-trade log via `RiskContext.Trades`). `Review` never sees auto-closes, so derive outcome state in `Supervise`, not `Review`.

## RiskContext

What a policy may read: `Position?`, `Capital`, `Bid`, `Ask`, `Cash`, `Leverage`, `Timestamp`, and `Trades` (the closed-trade log). Sizing/stop/reward math lives in `RiskHelper`.

## Reference policies

| Policy | Kind | Shows |
| --- | --- | --- |
| `MaxOpenRiskPolicy` | stateless gate | `Deny` a naked entry, `Modify` to clamp size within a risk cap. |
| `PercentStopLossPolicy` | stateless gate | `Modify` to attach a stop-loss when none is set. |
| `LossStreakVolumePolicy` | stateful | Track a loss streak in `Supervise` from `Trades`, halve entry size in `Review` until a non-losing trade resets it. |

## Notes

- A disabled policy's `Supervise` does not run; on re-enable it processes trades closed while it was off. Account for that if outcome history matters.
- Keep `Name` stable — it is the selection key persisted in a run's config and shown in logs.
