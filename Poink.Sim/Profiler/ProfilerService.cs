// ── Backward-chunked tick -> bar/volume-profile loader for ProfilerPage ──
//  Unlike SimulationEngine (stateful, tick-by-tick playback driving a live
//  Brain/Trader), Profiler only ever needs "bars ending at some point, going
//  back as far as the chart has actually been panned" -- so instead of
//  aggregating the whole selected range up front, LoadAsync pulls one
//  bar-aligned chunk at a time, newest-first, and the caller (ProfilerPage)
//  requests the next older chunk on demand as the user pans back through
//  history. Each chunk still streams straight from Postgres through the same
//  SimulationBarAggregator SimulationEngine uses, one tick at a time, keeping
//  only the resulting bars/histograms in memory rather than the chunk's raw
//  ticks (ITickRepository.ReadTicks is already a lazy, unbuffered reader).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Profiler;

internal sealed class ProfilerService
{
    // Matches SimulationEngine.DefaultPriceBucketWidth -- same seam noted there
    // for a future per-symbol lookup, kept duplicated rather than shared since
    // this is the only other place a BarPriceHistogram gets built.
    private const double DefaultPriceBucketWidth = 0.01;

    // Safety cap on how many empty/sparse sub-steps one LoadAsync call will walk
    // through (e.g. a run of weekend gaps) before giving up and returning
    // whatever it found -- bounds one JS-pan-triggered request to a bounded
    // number of Postgres round trips instead of possibly looping for a very
    // long time over a genuinely empty stretch of a table.
    private const int MaxSteps = 30;

    private readonly ITickRepository _tickRepository;
    private readonly ILogger<ProfilerService> _logger;

    public ProfilerService(ITickRepository tickRepository, ILogger<ProfilerService> logger)
    {
        _tickRepository = tickRepository;
        _logger = logger;
    }

    // Walks backward from beforeUtc, reading bar-aligned sub-chunks until either
    // barCount bars have been gathered, config.From is reached, or MaxSteps is
    // hit -- so a single gap (e.g. a weekend) doesn't come back as an empty
    // chunk the caller has to immediately re-request. beforeUtc is exclusive
    // (the chunk covers ticks strictly before it), matching ITickRepository.
    // ReadTicks' own [from, to) semantics.
    public Task<ProfilerChunk> LoadAsync(ProfilerConfig config, DateTime beforeUtc, int barCount) => Task.Run(() =>
    {
        if (barCount < 1) barCount = 1;
        var stepSpan = TimeSpan.FromTicks(Math.Max(config.BarSize.Ticks, 1) * barCount);

        var bars = new List<HlocAvdtBar>();
        var histograms = new List<BarPriceHistogram>();
        var tickCount = 0;
        var to = beforeUtc;
        var reachedStart = to <= config.From;

        for (var step = 0; step < MaxSteps && bars.Count < barCount && !reachedStart; step++)
        {
            var rawFrom = to - stepSpan;
            DateTime from;
            if (rawFrom <= config.From)
            {
                from = config.From;
                reachedStart = true;
            }
            else
            {
                // Snaps to the same bar-boundary grid SimulationBarAggregator uses
                // (tick.Ticks - tick.Ticks % barSize.Ticks) so this step's lower edge
                // lands exactly on a bar boundary -- that's what makes it safe to
                // flush this step's trailing in-progress bar below without ever
                // splitting a bar across two separately-aggregated steps.
                from = new DateTime(rawFrom.Ticks - (rawFrom.Ticks % config.BarSize.Ticks), rawFrom.Kind);
            }

            var aggregator = new SimulationBarAggregator(config.BarSize, DefaultPriceBucketWidth);
            var stepBars = new List<HlocAvdtBar>();
            var stepHistograms = new List<BarPriceHistogram>();

            foreach (var tick in _tickRepository.ReadTicks(config.Table, from, to))
            {
                tickCount++;
                var completed = aggregator.Feed(tick);
                if (completed is not null)
                {
                    stepBars.Add(completed.Value.Bar);
                    stepHistograms.Add(completed.Value.Histogram);
                }
            }

            // Every tick read above is strictly before `to`, and `to` is always
            // either bar-aligned (a prior step's own `from`) or the caller's outer
            // boundary (beforeUtc/config.To, past which nothing more will ever be
            // read) -- either way this step's trailing in-progress bar can't still
            // be waiting on ticks from some other step, so it's always safe to flush.
            if (aggregator.CurrentPartialBar is { } partial)
            {
                stepBars.Add(partial);
                stepHistograms.Add(aggregator.CurrentPartialHistogram!);
            }

            bars.InsertRange(0, stepBars);
            histograms.InsertRange(0, stepHistograms);
            to = from;
        }

        _logger.LogInformation(
            "Profiler chunk: table={Table}, before={Before}, earliest={Earliest}, ticks={TickCount}, bars={BarCount}, reachedStart={ReachedStart}",
            config.Table, beforeUtc, to, tickCount, bars.Count, reachedStart);

        return new ProfilerChunk(bars, histograms, to, tickCount, reachedStart);
    });
}
