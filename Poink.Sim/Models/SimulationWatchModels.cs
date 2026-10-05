using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Models;

/// <summary>A symbol replayed alongside the traded one, watch-only; its pip size sizes its price buckets.</summary>
internal sealed record SimulationWatchSymbol(string Table, double PipSize);

/// <summary>A watched symbol's market as of the traded symbol's current tick.</summary>
internal sealed record WatchedSymbolSnapshot(
	string Table,
	DateTime? CurrentTimestampUtc,
	double? CurrentBid,
	double? CurrentAsk,
	IReadOnlyList<HlocAvdtBar> RecentBars,
	IReadOnlyList<BarPriceHistogram> RecentBarHistograms,
	IReadOnlyList<Tick> RecentTicks
)
{
	public double? CurrentPrice => CurrentBid is { } bid && CurrentAsk is { } ask ? (bid + ask) / 2.0 : null;
}
