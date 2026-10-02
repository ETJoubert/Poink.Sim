using System;

namespace Poink.Sim.Components.Charts;

/// <summary>
/// One trade on an <see cref="EquityCurve"/>: <see cref="Equity"/> is the equity at the trade's
/// outcome, <see cref="TradeMaxEquity"/>/<see cref="TradeMinEquity"/> the best/worst equity reached
/// while it was open, and <see cref="TradeNumber"/> its 1-based sequence among all trades --
/// callers build these with <see cref="EquityCurveHelper"/>.
/// </summary>
public readonly record struct EquityCurveSample(
	DateTime TimestampUtc,
	double Equity,
	double TradeMaxEquity,
	double TradeMinEquity,
	int TradeNumber
);
