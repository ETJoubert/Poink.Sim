using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Models;

public enum SimulationAction
{
    Hold,
    EnterLong,
    EnterShort,
    Close,
}

internal enum OrderType
{
    Market,
    Limit,
    Stop,
}

internal readonly record struct SimulationDecision(
    SimulationAction Action,
    double CapitalFraction = 0,
    double? StopLossPrice = null,
    double? TakeProfitPrice = null,
    OrderType? OrderType = null,
    double? TriggerPrice = null,
    string? Reason = null
);

internal readonly record struct SimulationObservation(
    DateTime TimestampUtc,
    double CurrentPrice,
    IReadOnlyList<HlocAvdtBar> RecentBars,
    IReadOnlyList<Tick> RecentTicks,
    IReadOnlyList<BarPriceHistogram> RecentBarHistograms,
    CapitalState Capital,
    Position? Position,
    IReadOnlyList<Trade> RecentTrades,
    double Bid,
    double Ask
);

internal readonly record struct MarketWindow(
    DateTime TimestampUtc,
    double CurrentPrice,
    double Bid,
    double Ask,
    IReadOnlyList<HlocAvdtBar> RecentBars,
    IReadOnlyList<Tick> RecentTicks,
    IReadOnlyList<BarPriceHistogram> RecentBarHistograms
);

