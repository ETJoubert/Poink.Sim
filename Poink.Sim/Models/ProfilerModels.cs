using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

/// <summary>What to load for one Profiler symbol; <paramref name="PriceBucketWidth"/> sizes each bar's price histogram buckets.</summary>
internal sealed record ProfilerConfig(string Table, DateTime From, DateTime To, TimeSpan BarSize, double PriceBucketWidth = 0.01);

internal sealed record ProfilerChunk(
	IReadOnlyList<HlocAvdtBar> Bars,
	IReadOnlyList<QuartileBar> QuartileBars,
	IReadOnlyList<BarPriceHistogram> BarHistograms,
	DateTime EarliestUtc,
	int TickCount,
	bool ReachedStart
);
