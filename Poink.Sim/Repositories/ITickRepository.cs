// ── Tick data access contract ────────────────────────────────────────────
//  Raw (timestamp, bid, ask) ticks written by the Poink.Trader cAlgo robot
//  into Postgres (one table per symbol: ticks_<symbol>). Poink.Sim's own
//  copy -- Poink.ML has an identical one for its training features; kept
//  duplicated rather than shared so this project carries no Poink.ML
//  dependency (see Poink.ML.csproj's TorchSharp/Microsoft.ML weight).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Repositories;

internal readonly record struct Tick(DateTime Timestamp, double Bid, double Ask);

internal interface ITickRepository
{
    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables();

    IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null);
}
