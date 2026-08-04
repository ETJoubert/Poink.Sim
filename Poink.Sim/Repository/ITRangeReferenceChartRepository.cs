
using System;
using System.Collections.Generic;

namespace Poink.Sim.Repository;


public sealed record TRangeReferenceChart(
    long Id, string Label, string Symbol,
    DateTime StartTimeUtc, DateTime EndTimeUtc, double BarMinutes, int BarCount,
    DateTime ReferenceStartTimeUtc, DateTime ReferenceEndTimeUtc, int ReferenceBarCount,
    DateTime SavedAt)
{
    public TimeSpan Duration => EndTimeUtc - StartTimeUtc;
    public TimeSpan ReferenceDuration => ReferenceEndTimeUtc - ReferenceStartTimeUtc;
}

internal interface ITRangeReferenceChartRepository
{
    long SaveTRangeReferenceChart(
        string label, string symbol, DateTime startTimeUtc, DateTime endTimeUtc, double barMinutes, int barCount,
        DateTime referenceStartTimeUtc, DateTime referenceEndTimeUtc, int referenceBarCount);

    List<TRangeReferenceChart> ListTRangeReferenceCharts();

    void DeleteTRangeReferenceChart(long id);
}
