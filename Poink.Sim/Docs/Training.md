# Training — reinforcement learning for the Brain

> Doc version: **v1.0** — see "Changelog" at the bottom for what changed.
> See [README.md](README.md) for documentation standards.

## What it is

A brain starts out making random trades, plays thousands of headless
practice episodes against price data, and after each episode nudges its own
weights toward whatever decisions made money and away from whatever lost
money. No historical "correct answer" data is involved (that's what makes
it reinforcement learning instead of supervised learning) — it only ever
learns from its own simulated trading outcomes. The Train page (`/train`)
runs these practice sessions, with a Live tab showing reward improving (or
not) over time.

The abstraction underneath:

| Role | Interface | Analogy |
|---|---|---|
| A trader | `ISimulationBrain` (see [../Brain/README.md](../Brain/README.md)) | "someone who makes trading decisions" — a fixed rulebook or a trained network, the simulator doesn't care which |
| A trainable trader | `IPolicyGradientBrain` | a trader who can also say *why* they made a choice (a log-probability) — the one extra thing a coach needs |
| A coach | `IRlTrainingStrategy` | a method of turning practice results into improvement — one coaching style today (REINFORCE); a different one (e.g. a future DQN/actor-critic) is a new class + one DI registration |
| A scorecard | `IRewardPolicy` | what "doing well" means for one episode's steps — see "Reward policies" below |

`RlEpisodeRunner`/`RlTrainingService` wire a trader, a coach, and a
scorecard together without needing to know which specific ones they're
holding — that's the point of splitting them into separate, swappable
interfaces.

## Episodes

`RlEpisodeRunner.Run` headlessly plays one episode against an
already-materialized tick list — mirrors `SimulationEngine`'s tick loop
(one `Trader`, aggregate-or-raw observation building) but with no
Timer/locking/UI publish, so training can blast through many episodes fast.
It doesn't care where the ticks came from: `RlTrainingService` hands it
either synthetic ticks (`DummyTickGenerator`, shared with Simulation's "Load
Dummy Data") or a slice of real historic ticks.

For a historic run, `RlTrainingService.RandomEpisodeWindow` picks a random
start timestamp within the selected range and streams a bounded window from
there per episode (one chunk-pruned Postgres read per episode, never the
full range), so successive episodes train against different real market
windows without materializing more than one episode's worth of ticks at a
time.

**Warm-up bars.** A windowed encoder (e.g. `VolumeProfileEncoder`'s
lookback) needs real history before its first decision, not zero-padding.
`RlEpisodeRunner.Run` consumes `WarmupBars` worth of bars purely to fill the
recent-bars window before recording any `RlStep`/reward — warm-up bars
still land in the episode's bar/histogram output (so the Train page's
"current episode" chart shows the ramp-up), just with no decision/trade
possible.

**Reward** is computed once per decision point by whichever `IRewardPolicy`
the run is configured with (dense — available every step, not just at
episode end).

## Reward policies

`IRewardPolicy` (`{ Name, OnEpisodeStart(), ComputeReward(RewardContext) }`)
is a pure function of the step's capital/position transition plus the
market context around it (`Services/Training/RewardPolicies/`):

- **`EquityDriftRewardPolicy`** (default) — raw equity drift since the
  previous decision point, no shaping.
- **`TradingRulesRewardPolicy`** — same base, plus loss-aversion scaling on
  negative steps, an anti-churn cost when a new position opens, a growing
  penalty once a position's age exceeds a configured max holding duration,
  and a bonus for realizing a gain rather than letting it ride at risk.
- **`DifferentialSharpeRewardPolicy`** — Moody & Saffell's Differential
  Sharpe Ratio: rewards the step-by-step change in trailing risk-adjusted
  return via exponential mean/variance estimates, the one policy that uses
  `OnEpisodeStart`-managed state.
- **`MarketStructureRewardPolicy`** — same base, plus a bonus/penalty at
  entry from where price sits against the current Volume/Time Profile:
  favors continuing a breakout outside the Value Area when momentum agrees,
  or fading back to the Point of Control inside it.

`IRlTrainingStrategy`/`PolicyGradientTrainingStrategy` (REINFORCE with
discounted reward-to-go, `γ = 0.99`, and a mean-centered baseline) owns
turning whichever policy's raw per-step reward is into the actual loss —
the reward policy only decides *what* the number is.

## `RlTrainingService`

Background training orchestrator DI singleton — same `_lock`/`IsRunning`/
`Error`/`OnChanged` shape as `SimulationEngine`, but drives `RlEpisodeRunner`
in a tight loop instead of a UI-paced Timer, with one optimizer created for
the whole run (not per episode) so Adam's momentum persists correctly
across episodes.

- **Pause/Resume/Step** — `PauseTraining`/`ResumeTraining` gate the loop
  between episodes via a `ManualResetEventSlim` (no mid-episode checkpoint);
  `StepTraining` reopens the gate for exactly one episode then re-closes it,
  so a paused run can be advanced one episode at a time.
- **Brain Builder** (`TryBuildBrain`) — builds a runtime `ComposedPolicyBrain`
  from the Train page's Build Brain panel (encoders + architecture +
  hidden sizes + optional action-output heads + enabled directions) and
  registers it into the shared `BrainRegistry`, immediately visible to both
  Training and Simulation. The exact shape is kept as a `BrainRecipe` so it
  can be rebuilt after a process restart wipes it from `BrainRegistry`.
- **Save/Load** (`SaveBrain`/`LoadSavedBrain`) — persists a trained brain's
  exported weights plus its strategy/reward-policy/parameters/recipe via
  `IBrainRepository` into its own Postgres database (`Poink.Sim`, separate
  from the tick database). Every save inserts a new snapshot row, so
  training history stays browsable (`SavedBrainsTable`, shared with
  Simulation's brain picker). Loading a snapshot whose live brain no longer
  exists rebuilds it from its saved `BrainRecipe` first.
- **Rolling state for the Live tab** — `RecentEpisodes` (last 500, reward +
  final equity), `RecentEpisodeTrades`, `RecentEpisodeWindows` (historic
  runs only, feeds the data-coverage timeline), `ActionCounts`, best/worst/
  recent-average reward, and the most recently completed episode's full
  bar/histogram/trade/per-decision-input/output data (`LatestEpisode*`,
  overwritten every episode — pairs well with Step for inspecting exactly
  one episode at a time).

## Live training visuals

The Train page's Live tab shows what's actually happening, not just a
progress bar:

- **Stat tiles** — episodes done/total, recent average reward, best/worst
  episode, win rate.
- **`EpisodeGrid`** — one small block per completed episode, colored by
  reward sign/magnitude, with a redundant `+`/`−`/`·` glyph (color alone
  isn't trusted for sign — this app's success/error pair fails CVD
  separation).
- **`RewardChart`** — pure inline-SVG line of reward per episode, no JS.
- **`NetworkEmulator`** — a stylized (not literal) canvas animation of the
  selected brain's real layer shapes, pulsing between idle/busy depending on
  `IsRunning`.
- **Action distribution** — a labeled bar per `SimulationAction`, tallied
  from `ActionCounts`.
- **Current episode panel** — the most recently completed episode's bars via
  `DrawableOhlcvChart`, plus its Volume/Time Profile, with a PLAY/PAUSE +
  speed-stepper local replay that reveals bars one at a time and draws each
  closed trade as an entry-to-exit line once its exit bar is revealed.
- **INPUTS/OUTPUTS panels** (`BrainIoPanel`, shared with Simulation's own
  INPUTS/OUTPUTS tab) — synced to whichever bar the replay is currently on:
  INPUTS shows the network's raw feature values with a de-normalize toggle,
  OUTPUTS shows the sampled action's full probability distribution.
- **`EpisodeCoverageTimeline`** — a data-coverage heatmap over the selected
  historic range, shaded by how many recent episodes sampled each slice.

## Files

| Folder | File | Role |
|---|---|---|
| `Services/Training/` | `RlEpisode.cs` | `RlStep`/`RlEpisode`/`RlEpisodeMetric`/`RlActionCounts`/`RlDataSourceConfig` shapes |
| `Services/Training/` | `RlEpisodeRunner.cs` | Headless single-episode playback |
| `Services/Training/` | `IRlTrainingStrategy.cs`, `PolicyGradientTrainingStrategy.cs` | The "coach" contract + REINFORCE implementation |
| `Services/Training/` | `RlTrainingService.cs` | Background training orchestrator singleton |
| `Services/Training/RewardPolicies/` | `IRewardPolicy.cs` + 4 implementations | The "scorecard" contract + concrete policies (see "Reward policies" above) |
| `Repository/` | `IBrainRepository.cs`, `BrainRepository.cs` | Saved brain snapshot storage (weights + parameters + recipe) |
| `Components/Pages/Training/Pages/` | `TrainPage.razor(.cs/.css)` | The `/train` page (Setup/Live tabs, Build Brain panel) |
| `Components/Pages/Training/Pages/Train/` | `TrainLiveTab.razor(.cs/.css)` | The Live tab's full content (see "Live training visuals") |
| `Components/Pages/Training/Components/` | `EpisodeGrid/`, `RewardChart/`, `NetworkEmulator/`, `EpisodeCoverageTimeline/`, `BrainSelect/`, `BrainInfoCard/`, `SavedBrainsTable/` | The Live tab's building blocks, each its own component folder |

## Using it

`/train` — **Setup** tab: pick a trainable brain (or build one via
"+ BUILD BRAIN") + strategy + reward policy, episode count, ticks/bars per
episode, starting cash, learning rate, trader settings, then **Start
Training** (runs headlessly against synthetic ticks by default, or a
selected historic table/range). **Live** tab (jumped to automatically once
a run starts): stat tiles, episode grid, reward chart, network activity
animation, action distribution, current-episode replay with INPUTS/OUTPUTS,
and the episode metrics log. Pause/Resume/Step control the run from either
tab; Save Model persists the trained brain's weights for later use in
Simulation.

## Changelog

- **v1.0** — 2026-07-27 — Split out of the monolithic root README into its
  own doc, updated for the current `Services/Training` file layout, the
  `IRewardPolicy` scorecard abstraction, and Pause/Resume/Step controls.
