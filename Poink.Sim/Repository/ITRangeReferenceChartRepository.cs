// ── Saved T-Range Reference Chart storage ────────────────────────────────
//  Persists a labeled T-Range Reference Chart drawing (DrawableOhlcvChart's
//  T-RANGE REF CHART tool -- see ProfilerPage's Drawing.Kind) the same way
//  ITRangeChartRepository persists a plain T-Range Chart: Symbol + main
//  [StartTimeUtc, EndTimeUtc] + BarMinutes + BarCount is enough to redraw the
//  tracked range later by re-querying that symbol's ticks (see
//  ProfilerService.LoadAsync). A separate table/repository (not extra nullable
//  columns bolted onto trange_charts) since every row here always has a
//  reference window -- ReferenceStartTimeUtc/ReferenceEndTimeUtc/
//  ReferenceBarCount describe that window the same way the main four columns
//  describe the tracked range, so nothing here is ever null.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Repository;

// Public (not internal) since it's bound directly into DataTable's generic
// Items list from ProfilerPage.razor. Duration/ReferenceDuration are derived,
// not stored -- redundant with their Start/EndTimeUtc pairs, and DataTable's
// own reflection picks up get-only properties the same as any other, so they
// still show as columns.
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
