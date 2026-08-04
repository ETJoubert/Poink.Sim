
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record TransformerConfig(string Table, DateTime From, DateTime To, TimeSpan BarSize);

internal sealed record TransformerChunk(
    IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> BarHistograms,
    DateTime EarliestUtc, int TickCount, bool ReachedStart);
