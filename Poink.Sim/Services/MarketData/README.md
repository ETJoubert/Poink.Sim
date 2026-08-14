# MarketData Service

Tick ingestion for Poink.Sim: it keeps the local Postgres tick store (`PoinkMarketData`,
`ticks_<symbol>` tables) fed from cTrader, so both the simulation tooling and the live Trade page
read history the same way — from the database — while a live in-memory tail stays current from the
spot stream.

## Overview

The rich chart tooling (volume/time profiles, XGrid, overlays) is fed by one data shape: a rolling
window of `Tick` → `HlocAvdtBar` + `BarPriceHistogram`, produced by `SimulationBarAggregator`. That
aggregator is source-agnostic — it consumes ticks and does not care whether they were replayed from
Postgres or streamed live. This service is the machinery that gets **real** ticks into the store so
that pipeline can run against real market data.

Design principles:

- **The database is the source of truth for history.** Reads go through `ITickRepository`
  (Repository/), shared with the simulation. This service only writes.
- **Single writer.** `TickBackfillWorker` is the only component that writes tick tables. The live
  spot stream never writes directly; it fills an in-memory buffer the worker drains.
- **API pays only for the gap.** cTrader `ProtoOAGetTickData` fills the pre-stream gap between what
  the DB already has and when the stream started; everything from stream-start forward is persisted
  from memory for free.
- **Coverage stays contiguous.** The worker never persists buffered stream ticks until the DB is
  contiguous up to the stream's start, which keeps the min/max `tick_table_stats` registry honest.

## As Is

### Pieces

| Type | Role |
|---|---|
| `MarketDataModels` | `MarketTick`, `TickCoverage`, `TickIngestState`, `SymbolIngestStatus`, `TickTableStatRow`, `BackfillTrigger`, `BackfillLogEntry`. |
| `TickTableNaming` | Symbol → table convention: `ticks_` + symbol lower-cased, non-alphanumerics stripped. |
| `ITickIngest` / `TickIngest` | Write side. Create table, coverage, bulk `COPY` insert (staged, `ON CONFLICT` skip-duplicate), `tick_table_stats` upserts, symbol association, registry read, `tick_backfill_log` write/read. Separate from the read-only `ITickRepository`. |
| `IHistoricalTickSource` / `CTraderHistoricalTickSource` | Historical-fetch seam. The real one delegates to the session's `ICTraderHistory`; available only while connected with a selected account. |
| `TickDataDecoder` | `PricePoint`; `Decode` (reverse the newest-first, delta-encoded response) and `Merge` (bid + ask → mid `Tick`). |
| `ITickBackfillController` / `TickBackfillWorker` | Hosted `BackgroundService` + control surface. Buffers live ticks, runs the per-symbol state machine, exposes status and controls. |

Cross-project dependencies:

- `CTraderSessionService` (Services/CTrader) emits `TickReceived` per watched symbol's merged
  bid/ask, and implements `ICTraderHistory.GetHistoricalTicksAsync` (paged bid+ask fetch, decode,
  merge) under a per-connection historical-rate throttle.
- `CTraderClient.GetTickDataAsync` (Poink.CTrader) is the one-chunk request following the
  `SendAndWaitAsync` pattern.
- The Data page (`Components/Pages/Data`) is the monitor + control UI (INGEST / REGISTRY tabs).

### Per-symbol state machine (`TickBackfillWorker`)

```
NoTable ─▶ Idle ─▶ ClosingGap ─▶ DrainingBuffer/TailingLive
```

- **NoTable** — symbol tracked but no table (creation is explicit opt-in).
- **Idle** — table present, no ticks seen yet.
- **ClosingGap** — `db.max_timestamp < streamStart`. Fetches `[max, streamStart)` via the history
  source, filtered to `> max`, retried at most every 5s.
- **DrainingBuffer / TailingLive** — gap closed (or table empty, so no gap). Buffered ticks `> max`
  are inserted; new stream ticks batch-flush each cycle (1s).

Invariant: buffered stream ticks are only persisted once the DB is contiguous to `streamStart`.

### TimescaleDB hypertables

`TickIngest.CreateTable` converts every newly created `ticks_<symbol>` table into a TimescaleDB
hypertable (`create_hypertable(table, 'timestamp', if_not_exists => true)`), chunked on `timestamp`,
right after creating it — safe because `CreateTable` only ever runs against a table confirmed not to
exist yet, so there is never data to migrate. The Poink.Trader cAlgo robot's `OnStart` does the same for
its own table, but that path is not creation-gated (it re-runs on every start against a possibly
already-populated table), so its hypertable conversion is wrapped to log and continue rather than throw
when the target already holds rows — converting an already-populated table needs a deliberate, manual
`create_hypertable(..., migrate_data => true)` run, not something either ingestion path triggers on its
own.

### Tuning constants

- Flush cycle: 1s. Gap retry: 5s. Historical request spacing: 220ms (~4.5/sec).
- Persistence buffer cap: 500k ticks, trimmed 4096 at a time. Max paged tick requests per side: 200.

### Backfill audit (`tick_backfill_log`)

Every completed backfill fetch-and-store logs one row: `tick_table_name`, `symbol_code` (cTrader
`SymbolId`), `symbol_name`, `backfill_from`/`backfill_to` (the requested window, not just the span of
ticks actually returned), `record_count`, `trigger_entity` (`User` or `Worker`), `triggered_at`. Two call
sites write it: `TickBackfillWorker.BackfillRangeAsync` (Data page's Load Historic control, `User`) and
the `ClosingGap` branch of `ProcessJobAsync` (the worker's own pre-stream gap-fill, `Worker`). A
zero-tick `ClosingGap` attempt is not logged — it retries every 5s and would otherwise flood the table
across a closed market; a zero-tick manual backfill is logged, since it is a single explicit action.

### Controls (Data page)

- Global pause/resume of the worker.
- Track & Stream (creates table on opt-in, subscribes spots, persists).
- Force flush, untrack. REGISTRY tab shows raw `tick_table_stats`.
- Load Historic (REGISTRY tab, per row): fetches N days of ticks older than that table's `MinUtc` via
  `GetTickData` and stores them. The symbol is resolved from the registry's `symbol` column against the
  connected account's symbol list, so it requires both a connected session and an existing, symbol-mapped
  table.

## Gaps

- **Delta-decode sign unverified.** `TickDataDecoder.Decode` accumulates deltas per cTrader's
  documented scheme, but the sign was not confirmed against a live response. A wrong sign surfaces as
  a backfill returning zero (reconstructed timestamps fall outside the requested window).
- **Load Historic blocks the UI.** It runs synchronously on the Blazor circuit; a large day count holds
  that row's control in a busy state for the duration. Not yet on the worker's background queue.
- **No rate-limit retry.** The 220ms throttle is proactive only; a rate-limit `CTraderApiException`
  is not caught and retried with backoff.
- **Buffer-overflow leaves a hole.** On cap, the oldest buffered ticks are trimmed and `streamStart`
  advances; once history is available the skipped span is not re-fetched, so a gap can persist.
- **Ingest needs both tracked and watched.** Ticks flow only for symbols that are tracked *and*
  spot-subscribed (watched). Unwatching stops ingestion; jobs are not auto-untracked on disconnect.
- **No table auto-creation** (by design) and **no automatic legacy-table linking** — existing,
  non-conventional tables must be linked once by hand.
- **No tests.**

## Concerns

- **Historical rate budget (5/sec/connection).** `GetTickData` and `GetTrendbars` share it. The
  throttle keeps the worker legal, but a busy pair over a wide range is many paged requests and runs
  slowly; other historical callers on the same connection eat the same budget.
- **Response cross-talk.** `SendAndWaitAsync` matches by response type, so two concurrent same-type
  historical calls would resolve against each other's response. `GetTickData` is serialized by the
  session's `_historyGate`; `GetTrendbars` is not, relying on the UI issuing one chart load at a time.
- **Memory pressure.** The persistence buffer must retain everything since `streamStart` until the
  gap closes. A stale table + busy symbol + rate-limited API can approach the 500k cap before that.
- **Timestamp regime.** Live ticks prefer the spot event's server timestamp but fall back to arrival
  time when absent; a fallback near a backfill join can slightly disorder ticks at the seam.
- **Same-ms boundary.** Flushes filter `> max_timestamp`, so a tick sharing the exact millisecond of
  the current max can be dropped at a boundary.
- **Shared database.** `PoinkMarketData` is read by `TickRepository` (and possibly other tools) while
  this service writes; heavy backfills contend with those reads.
- **Loop-thread DB I/O.** `TickBackfillWorker` processes jobs sequentially on its loop; a slow flush
  for one symbol delays every other symbol that cycle.
- **Symbol↔table mapping fragility.** The convention and the registry `symbol` column both assume
  tables match `LIKE 'ticks_%'`; a table without that prefix would be dropped from the registry by a
  stats refresh, taking its mapping with it.
