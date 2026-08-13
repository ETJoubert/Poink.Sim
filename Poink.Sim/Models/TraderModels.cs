using System;

namespace Poink.Sim.Models
{
    // CTrader's HLOC/AVDT bar data, as returned by the REST API and stored in Postgres.
    // See Poink.CTrader/Models/CTraderBar.cs for the original source of truth.
    public sealed record CTraderBarRow(
            DateTime TimeUtc, double Open, double High, double Low, double Close, long Volume,
            long? UpVolume = null, long? DownVolume = null, double? Distance = null);

}
