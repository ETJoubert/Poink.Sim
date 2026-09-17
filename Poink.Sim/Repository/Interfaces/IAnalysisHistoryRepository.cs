using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

public sealed record AnalysisHistorySeriesSummary(string Series, long ReturnedCount, double ReturnProbability, double MeanToReturn, double MedianToReturn);

// Nullable columns cover both analysis types in one row -- a Time Above/Below save leaves the
// Return Probability columns null and vice versa, so this stays a single reporting-friendly table.
public sealed record AnalysisHistoryRecord(
	long Id,
	string AnalysisType,
	string Label,
	string TableName,
	DateTime FromUtc,
	DateTime ToUtc,
	double? WindowMinutes,
	int? AggregationBarWidthSeconds,
	int? AggregationWindowBars,
	string? AggregationKind,
	long SampleCount,
	long? ReturnedCount,
	double? ReturnProbability,
	double? MeanToReturn,
	double? MedianToReturn,
	string? ToReturnUnit,
	double? MeanFractionAbove,
	double? MedianFractionAbove,
	double? StdDevFractionAbove,
	double? MinFractionAbove,
	double? MaxFractionAbove,
	IReadOnlyList<AnalysisHistorySeriesSummary>? SeriesBreakdown,
	DateTime SavedAtUtc
);

public sealed record AnalysisHistoryBin(string? Series, int BinIndex, double Low, double High, long Count);

public sealed record AnalysisHistoryDetail(AnalysisHistoryRecord Record, IReadOnlyList<AnalysisHistoryBin> Bins);

internal interface IAnalysisHistoryRepository
{
	long SaveTimeAboveBelow(string label, TimeAboveBelowRequest request, TimeAboveBelowDistributionResult result);

	long SaveReturnProbability(string label, PriceReturnRequest request, PriceReturnDistributionResult result);

	List<AnalysisHistoryRecord> ListHistory();

	AnalysisHistoryDetail GetDetail(long id);

	void DeleteHistory(long id);
}
