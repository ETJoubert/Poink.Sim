using System;
using System.Collections.Generic;

namespace Poink.Sim.Repository;

public sealed record TRangeChart(
	long Id,
	string Label,
	string Symbol,
	DateTime StartTimeUtc,
	DateTime EndTimeUtc,
	double BarMinutes,
	int BarCount,
	DateTime SavedAt
)
{
	public TimeSpan Duration => EndTimeUtc - StartTimeUtc;
}

internal interface ITRangeChartRepository
{
	long SaveTRangeChart(
		string label,
		string symbol,
		DateTime startTimeUtc,
		DateTime endTimeUtc,
		double barMinutes,
		int barCount
	);

	List<TRangeChart> ListTRangeCharts();

	void DeleteTRangeChart(long id);
}
