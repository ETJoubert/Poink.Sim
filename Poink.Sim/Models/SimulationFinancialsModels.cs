using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

/// <summary>
/// Every trader's account figures and the quote they were valued at -- published at max speed too, when no full
/// snapshot is, so the Financials panel can keep updating on its own.
/// </summary>
internal sealed record SimulationFinancialsState(
	IReadOnlyList<TraderSnapshot> Traders,
	string Table,
	double PipSize,
	double? Bid,
	double? Ask,
	double? Price,
	DateTime? TimestampUtc
);
