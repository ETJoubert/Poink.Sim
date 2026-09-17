using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record PriceReturnRequest(
	string Table,
	DateTime From,
	DateTime To,
	double WindowMinutes,
	int? AggregationBarWidthSeconds = null,
	int? AggregationWindowBars = null,
	BarAggregationKind AggregationKind = BarAggregationKind.Ohlc);

// ToReturnUnit is "min" (tick mode) or "bars" (aggregation -- no clock, just bar positions).
internal sealed record PriceReturnDistributionResult(
	long SampleCount,
	long ReturnedCount,
	double ReturnProbability,
	double MeanToReturn,
	double MedianToReturn,
	IReadOnlyList<HistogramBin> ToReturnBins,
	string ToReturnUnit,
	IReadOnlyList<PriceReturnSeriesResult>? SeriesBreakdown = null);

internal sealed record PriceReturnSeriesResult(
	string Series,
	long ReturnedCount,
	double ReturnProbability,
	double MeanToReturn,
	double MedianToReturn,
	IReadOnlyList<HistogramBin> ToReturnBins);
