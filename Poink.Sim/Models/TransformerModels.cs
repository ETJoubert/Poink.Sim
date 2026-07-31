// ── Read-model for a batch (non-playback) OHLC + volume/time-profile view ───
//  TransformerPage loads its selected symbol/date range in bar-aligned chunks,
//  newest-first, backfilling older history on demand as the chart is panned
//  (see Transformer/TransformerService.cs) -- no Simulation-style tick-by-tick
//  playback, brain, or trader involved, just "here are this chunk's bars and
//  their price histogram". Mirrors ProfilerModels.cs -- see ProfilerPage for
//  the drawing/save features this page deliberately leaves out.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record TransformerConfig(string Table, DateTime From, DateTime To, TimeSpan BarSize);

// One backward chunk load's result -- EarliestUtc is the cursor to pass as the
// next call's `beforeUtc` to keep walking further back; ReachedStart is true
// once that cursor has hit Config.From, meaning there's nothing older left to load.
internal sealed record TransformerChunk(
    IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> BarHistograms,
    DateTime EarliestUtc, int TickCount, bool ReachedStart);
