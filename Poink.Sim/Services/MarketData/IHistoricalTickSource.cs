using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData
{
    /// <summary>
    /// Fetches historical ticks for the pre-stream gap the backfill worker cannot fill from the live
    /// stream. Backed by cTrader's ProtoOAGetTickData (bid + ask series merged into mid quotes) once
    /// implemented; the current stub returns nothing, so only empty or already-current tables persist.
    /// </summary>
    internal interface IHistoricalTickSource
    {
        bool IsAvailable { get; }

        Task<IReadOnlyList<Tick>> GetTicksAsync(long symbolId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    }
}
