
using System;
using System.Collections.Generic;

namespace Poink.Sim.Repository;

internal readonly record struct Tick(DateTime Timestamp, double Bid, double Ask);

internal interface ITickRepository
{
    bool HasCachedTables { get; }


    IReadOnlyList<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> CachedTables { get; }


    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables();


    List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> RefreshTickTableStats();

    IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null);

    List<Tick> ReadWindow(string table, DateTime start, int count);


    List<Tick> ReadRecentTicks(string table, DateTime beforeUtc, int count);


    long CountTicks(string table, DateTime from, DateTime to);
}
