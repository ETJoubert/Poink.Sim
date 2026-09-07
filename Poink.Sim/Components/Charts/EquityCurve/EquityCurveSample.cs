using System;

namespace Poink.Sim.Components.Charts;

/// <summary>
/// One point on an <see cref="EquityCurve"/>. <see cref="TradeMaxEquity"/>/<see cref="TradeMinEquity"/>
/// carry the best/worst equity reached since the currently open trade's entry, and <see cref="TradeNumber"/>
/// its 1-based sequence among all trades; all three are null when no trade is open at
/// <see cref="TimestampUtc"/> -- callers build these with <see cref="EquityCurveHelper"/>.
/// </summary>
public readonly record struct EquityCurveSample(
	DateTime TimestampUtc,
	double Equity,
	double? TradeMaxEquity,
	double? TradeMinEquity,
	int? TradeNumber
);
