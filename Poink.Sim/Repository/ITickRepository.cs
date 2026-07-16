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
    // Cheap read of the cached tick_table_stats registry -- no Postgres scanning.
    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables();

    // Rescans every ticks_* table (COUNT(*)/MIN/MAX -- the expensive part) and
    // upserts the results into tick_table_stats, then returns the refreshed list.
    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> RefreshTickTableStats();

    IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null);
}
