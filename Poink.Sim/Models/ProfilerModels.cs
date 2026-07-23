// ── Read-model for a batch (non-playback) OHLC + volume-profile view ────
//  ProfilerPage loads its selected symbol/date range in bar-aligned chunks,
//  newest-first, backfilling older history on demand as the chart is panned
//  (see Profiler/ProfilerService.cs) -- no Simulation-style tick-by-tick
//  playback, brain, or trader involved, just "here are this chunk's bars and
//  their price histogram".
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal sealed record ProfilerConfig(string Table, DateTime From, DateTime To, TimeSpan BarSize);

// One backward chunk load's result -- EarliestUtc is the cursor to pass as the
// next call's `beforeUtc` to keep walking further back; ReachedStart is true
// once that cursor has hit Config.From, meaning there's nothing older left to load.
internal sealed record ProfilerChunk(
    IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> BarHistograms,
    DateTime EarliestUtc, int TickCount, bool ReachedStart);
