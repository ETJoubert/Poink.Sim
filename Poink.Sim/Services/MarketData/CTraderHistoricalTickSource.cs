using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.MarketData
{
    /// <summary>
    /// Real historical source backed by cTrader ProtoOAGetTickData via the authorized session connection.
    /// Available only while the session is connected with a selected account.
    /// </summary>
    internal sealed class CTraderHistoricalTickSource : IHistoricalTickSource
    {
        private readonly ICTraderHistory _history;

        public CTraderHistoricalTickSource(ICTraderHistory history)
        {
            _history = history;
        }

        public bool IsAvailable => _history.CanFetchHistory;

        public Task<IReadOnlyList<Tick>> GetTicksAsync(long symbolId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            _history.GetHistoricalTicksAsync(symbolId, fromUtc, toUtc, cancellationToken);
    }
}
