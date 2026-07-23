// ── Saved T-Range Chart storage ──────────────────────────────────────────
//  Persists a labeled T-Range Chart drawing (DrawableOhlcvChart's T-RANGE
//  CHART tool -- see ProfilerPage's Drawing.Kind) as just the range it marks,
//  not a bar/line snapshot: Symbol + [StartTimeUtc, EndTimeUtc] + BarMinutes
//  + BarCount is enough to redraw it later by re-querying that symbol's
//  ticks for the same window (see ProfilerService.LoadAsync, called with
//  beforeUtc=EndTimeUtc and count=BarCount to get exactly this many bars
//  back -- the same aggregation the live chart itself used when the range
//  was first drawn). Same Postgres database as IBrainRepository ("Poink.Sim",
//  not "Poink.MarketData") since neither is market data.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Repository;

// Public (not internal) since it's bound directly into DataTable's generic
// Items list from ProfilerPage.razor. Duration is derived, not stored --
// redundant with Start/EndTimeUtc, and DataTable's own reflection picks up
// get-only properties the same as any other, so it still shows as a column.
public sealed record TRangeChart(
    long Id, string Label, string Symbol, DateTime StartTimeUtc, DateTime EndTimeUtc, double BarMinutes, int BarCount, DateTime SavedAt)
{
    public TimeSpan Duration => EndTimeUtc - StartTimeUtc;
}

internal interface ITRangeChartRepository
{
    long SaveTRangeChart(string label, string symbol, DateTime startTimeUtc, DateTime endTimeUtc, double barMinutes, int barCount);

    List<TRangeChart> ListTRangeCharts();

    void DeleteTRangeChart(long id);
}
