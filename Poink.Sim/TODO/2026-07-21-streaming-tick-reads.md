# Streaming tick reads (drop the full-range in-memory preload)

Status: Done

## Why

`TickDataStore.TryLoad` currently materializes the *entire* selected
table+range into one `List<Tick>` before Simulation or Training ever touch
it (see `TickRepository.ReadTicks(...).ToList()`). With `ticks__usndaq100`
at 161.8M rows, that's several GB in one Blazor Server process -- larger
than available RAM for any serious backtest/training range. This was the
actual scaling wall; Redis caching (tried and removed) only ever cached a
copy of that same too-big object, so it never could have fixed this.

The TimescaleDB migration (done -- see `docker-compose.yml`, both
`ticks_*` tables are now hypertables) is what makes the fix below viable:
a `WHERE timestamp BETWEEN x AND y` query now only scans the chunks that
overlap the range instead of the whole table.

## Scope

Two independent changes. Training first (smaller, contained, fixes the
worse offender since RL blows through many episodes); Simulation streaming
second (bigger, touches `SimulationEngine`'s playback loop).

### 1. Training: per-episode windowed query instead of a full-range preload

Today: `TrainSetupTab` preloads the whole historic range into
`TickDataStore`, then `RlTrainingService.RandomWindow` slices a random
`ticksPerEpisode`-sized chunk out of that in-memory list, once per episode.

Each episode only ever needs `ticksPerEpisode` rows (e.g. 500) -- there's
no reason to hold the full range in memory just to cut one small window out
of it.

Change:
- Add `ITickRepository.ReadWindow(string table, DateTime start, int count)`
  -- `SELECT ... WHERE timestamp >= @start ORDER BY timestamp LIMIT @count`
  (chunk-pruned by Timescale, cheap regardless of table size).
- `RlTrainingService`/`RlEpisodeRunner` picks a random start timestamp
  within the table's known `[min, max]` (already known via
  `tick_table_stats`, no scan needed) and calls `ReadWindow` directly, once
  per episode, instead of slicing a preloaded list.
- `TrainSetupTab` no longer needs a bulk "LOAD DATA" step for the historic
  path -- picking a symbol + range is enough; drop the `IsDataReady`/
  `TickDataStore` gating for training specifically. (Simulation keeps its
  own "LOAD DATA" step until part 2 lands.)
- `RlTrainingService.TryStartTraining`'s `List<Tick>? historicTicks`
  parameter becomes a `(string Table, DateTime From, DateTime To)?` config
  instead.

### 2. Simulation: bounded streaming playback instead of a full-range preload

Today: `SimulationEngine` indexes a fully materialized `List<Tick> _ticks`
via an integer cursor (`_ticks![_cursor]`), which is what "random access
for pause/step/speed" wanted -- but actual playback only ever moves the
cursor *forward* (except `Reset`, which rewinds to the start).

Change:
- Replace `List<Tick> _ticks` + `int _cursor` with a live, forward-only
  reader over `ITickRepository.ReadTicks` (already a lazy
  `IEnumerable<Tick>` under Dapper's `buffered: false` -- `TickDataStore`
  is what forces it into a list today, not `TickRepository` itself).
- `Reset` disposes and reopens the reader from the start of the configured
  range.
- Total tick count for the progress bar switches from `_ticks.Count` to a
  cheap upfront `COUNT(*) WHERE timestamp BETWEEN ...` at load time
  (chunk-pruned, not a full scan).
- The existing bounded ring buffers (`_recentTicks`/`_recentBars`,
  `MaxRecentTicks`/`MaxRecentBars`) already cap what's kept for chart
  display -- unaffected.
- `LoadDummySimulation`'s synthetic-tick path is unaffected (never touched
  Postgres).

## Non-goals

- Compression policies on the hypertables (separate, optional follow-up).
- Any change to brain/training algorithms themselves.
- Re-introducing Redis or any other cache layer.

## Verification

- Start a full-range historic training run against `ticks__usndaq100` and
  confirm process memory stays flat across many episodes (previously this
  range couldn't even be loaded).
- Start a Simulation run against a wide range, confirm Play/Pause/Step/
  speed controls and the progress bar still behave correctly, and Reset
  correctly rewinds.
- Confirm `LoadDummySimulation`/synthetic training episodes still work
  unchanged.
