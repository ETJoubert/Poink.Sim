using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record PriceReturnRequest(string Table, DateTime From, DateTime To, double WindowMinutes);

internal sealed record PriceReturnDistributionResult(
	long SampleCount,
	long ReturnedCount,
	double ReturnProbability,
	double MeanMinutesToReturn,
	double MedianMinutesToReturn,
	IReadOnlyList<HistogramBin> MinutesToReturnBins);
