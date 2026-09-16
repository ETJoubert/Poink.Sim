using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record TimeAboveBelowRequest(string Table, DateTime From, DateTime To, double WindowMinutes);

internal readonly record struct HistogramBin(double Low, double High, long Count);

internal sealed record TimeAboveBelowDistributionResult(
	long SampleCount,
	double MeanFractionAbove,
	double MedianFractionAbove,
	double StdDevFractionAbove,
	double MinFractionAbove,
	double MaxFractionAbove,
	IReadOnlyList<HistogramBin> Bins);
