using System;

namespace Poink.Sim.Components.Charts;

/// <summary>
/// One point on an <see cref="EquityCurve"/>. <see cref="TradeMaxEquity"/>/<see cref="TradeMinEquity"/>
/// carry the best/worst equity reached since the currently open trade's entry, or null when no trade
/// is open at <see cref="TimestampUtc"/> -- callers build these with <see cref="EquityCurveHelper"/>.
/// </summary>
public readonly record struct EquityCurveSample(
	DateTime TimestampUtc,
	double Equity,
	double? TradeMaxEquity,
	double? TradeMinEquity
);
