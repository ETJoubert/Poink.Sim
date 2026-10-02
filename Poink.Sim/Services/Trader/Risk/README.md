# Risk

Additive risk-supervision layer that sits between "something proposes a trade action" and the trader acting on it, plus a per-tick watchdog over the open position. Applies to both simulation and training with no call-site changes.

## Where it plugs in

`TraderFactory.Create` wraps every core `Trader` in a `RiskAwareTrader` decorator when the run names any policies. Because the decorator implements `ITrader`, `SimulationEngine` and `RlEpisodeRunner` are unchanged.

```
RiskAwareTrader : ITrader   ->   RiskManager   ->   PositionManager   ->   core Trader
     (decorator)                  (policies)         (position seam)       (unchanged)
```

## Two hooks

- **Gate** — `RiskAwareTrader.Apply` calls `RiskManager.Gate` before the core trader acts. Each active policy's `Review` runs in order: the first `Deny` is terminal; a `Modify` feeds its changed decision into the next policy. A denied action returns a `TradeExecution { Blocked = true }` (surfaced in the decision log and the training blocked-metric); otherwise the core trader applies the reviewed decision.
- **Supervise** — `RiskAwareTrader.Advance` runs the core maintenance first (stop-loss/take-profit/stop-out, untouched), then `RiskManager.Supervise`. Each active policy may inject its own decisions, executed on the core trader and folded into `MaintenanceResult.AutomaticFills`. Supervised actions bypass the gate, being the policy's own.

## Pieces

| Type | Role |
| --- | --- |
| `IRiskPolicy` | One rule. `Name`, `Description`, `Fresh()`, `Review`, `Supervise`. See `Policies/README.md`. |
| `RiskContext` | Read-only snapshot a policy reasons over: position, capital, bid/ask, cash, leverage, timestamp, closed-trade log. |
| `RiskReview` | Verdict (`Permit`/`Deny`/`Modify`) + resulting decision + reason. Build via `RiskReview.Permit/Deny/Modify`. |
| `IPositionManager` / `PositionManager` | The seam over the core `ITrader`: builds a `RiskContext` and executes decisions. |
| `IRiskManager` / `RiskManager` | Runs the gate and the supervisor over a trader's active policies. Skips a policy disabled in the run's toggles. |
| `RiskAwareTrader` | The `ITrader` decorator. |
| `IRiskPolicyRegistry` / `RiskPolicyRegistry` | DI-seeded catalog (mirrors `BrainRegistry`). `AvailablePolicies` for the UI; `CreateActive(names)` mints a per-trader instance of each selected policy via `Fresh()`. |
| `IRiskManagerFactory` / `RiskManagerFactory` | Assembles the decorator from the registry's active policies. |
| `RiskPolicyToggles` | Run-level, thread-safe enable/disable per policy name, shared by every trader in the run. |
| `RiskHelper` | Stateless sizing/stop/reward math shared by policies and the trader. |

## Discovery

`AddRiskPolicies()` (in `RiskPolicyServiceCollectionExtensions`) reflects over this assembly, registers every concrete `IRiskPolicy`, then registers the registry and manager factory. A new policy needs only its class — no registration line. `Program.cs` calls it once.

## Selection and toggling

- Selection is per run: `SimulationConfig.RiskPolicyNames` (sim) and the `TraderSettings.RiskPolicyNames` the training path builds. `TraderSettings.RiskToggles` carries the run's shared `RiskPolicyToggles`.
- Toggling is **global for the run**: `SimulationEngine.SetRiskPolicyEnabled` flips one shared `RiskPolicyToggles`, so every trader's `RiskManager` sees it on the next tick.
- Policies are never locked: before a run the right panel's Policies tab edits the selection, and during a run `RiskPolicyPanel` toggles, reorders and retunes them live.
- `SimulationSnapshot.RiskPolicies` (`RiskPolicySnapshot`) feeds that panel.
