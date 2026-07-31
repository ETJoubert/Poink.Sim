// ── Tick data access contract ────────────────────────────────────────────
//  Raw (timestamp, bid, ask) ticks written by the Poink.Trader cAlgo robot
//  into Postgres (one table per symbol: ticks_<symbol>). Poink.Sim's own
//  copy -- Poink.ML has an identical one for its training features; kept
//  duplicated rather than shared so this project carries no Poink.ML
//  dependency (see Poink.ML.csproj's TorchSharp/Microsoft.ML weight).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Repository;

internal readonly record struct Tick(DateTime Timestamp, double Bid, double Ask);

internal interface ITickRepository
{
    // True once ListTickTables()/RefreshTickTableStats() has populated CachedTables
    // at least once this run. ITickRepository is a singleton, so pages can check
    // this on their own OnInitialized to reuse a load another page already did,
    // instead of making the user click a data button on every page they visit.
    bool HasCachedTables { get; }

    // In-memory copy of the last ListTickTables()/RefreshTickTableStats() result.
    IReadOnlyList<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> CachedTables { get; }

    // Cheap read of the cached tick_table_stats registry -- no Postgres scanning.
    // Populates CachedTables.
    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables();

    // Rescans every ticks_* table (COUNT(*)/MIN/MAX -- the expensive part) and
    // upserts the results into tick_table_stats, then returns the refreshed list.
    // This is the only thing that ever rescans Postgres -- call it explicitly
    // (a "Recount" action), not as part of the normal data-loading path.
    // Populates CachedTables.
    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> RefreshTickTableStats();

    IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null);

    // Bounded window read for a single training episode -- chunk-pruned by
    // Timescale regardless of table size, so this stays cheap even against
    // a 160M-row table. Materialized (not lazy) since callers always need
    // the whole bounded window in hand (e.g. RlEpisodeRunner.Run).
    List<Tick> ReadWindow(string table, DateTime start, int count);

    // Backward counterpart to ReadWindow -- the last `count` ticks at or before
    // `beforeUtc`, still returned in ascending (chronological) order. Same
    // chunk-pruned-by-LIMIT cheapness as ReadWindow, just walking the other
    // direction (see TransformerPage's tick-delta chart, the only current caller).
    List<Tick> ReadRecentTicks(string table, DateTime beforeUtc, int count);

    // Cheap COUNT(*) over a chunk-pruned range -- used for progress bars
    // instead of materializing the range just to know its length.
    long CountTicks(string table, DateTime from, DateTime to);
}
