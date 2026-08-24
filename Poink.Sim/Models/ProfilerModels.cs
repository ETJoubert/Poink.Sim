using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record ProfilerConfig(string Table, DateTime From, DateTime To, TimeSpan BarSize);

internal sealed record ProfilerChunk(
	IReadOnlyList<HlocAvdtBar> Bars,
	IReadOnlyList<BarPriceHistogram> BarHistograms,
	DateTime EarliestUtc,
	int TickCount,
	bool ReachedStart
);
