// ── Local (per-window) Z-Score series builder ────────────────────────────
//  Best for pattern matching: rather than TickDeltaTransform's single fixed
//  reference point (the latest tick), this partitions a chronologically-
//  ordered tick list into consecutive, non-overlapping windows of windowSize
//  ticks each, then re-expresses every tick in a window relative to that
//  window's OWN mean (mu_w) and standard deviation (sigma_w):
//
//      z_i = (p_i - mu_w) / sigma_w
//
//  Every window comes out zero-mean, unit-variance, independent of every
//  other window -- so a network trained on these series learns pure
//  geometric shape (head-and-shoulders, double bottoms, momentum squeezes)
//  decoupled from macro trend or long-term price inflation, instead of
//  learning absolute price levels. windowSize must match the caller's
//  SimulationBarAggregator bar size (in the same synthetic tick-count units
//  TickDeltaTransform uses) so each resulting bar reflects exactly one
//  sub-window's own normalization -- see TransformerPage's Local Z-Score
//  chart, the only current caller. Contrast with GlobalWindowZScoreTransform,
//  which treats the entire input as a single window instead of partitioning it.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Transformer;

internal static class LocalWindowZScoreTransform
{
    // Same arbitrary synthetic spacing as TickDeltaTransform -- SimulationBarAggregator
    // only cares about relative ordering/spacing, not real elapsed time.
    private static readonly TimeSpan TickUnit = TimeSpan.FromSeconds(1);

    // Below this, a window is treated as flat (z=0 for all of it) rather than
    // standardized -- guards not just exact zero variance but also the floating-point
    // noise a "flat" window's mean/sumSquares math can still leave behind, which would
    // otherwise divide by a near-zero stdDev and blow up into a huge z-score.
    private const double FlatWindowEpsilon = 1e-8;

    // ticks must already be in ascending chronological order (see
    // ITickRepository.ReadTicks/ReadRecentTicks). Returns one entry per input tick.
    public static List<Tick> BuildZScoreSeries(IReadOnlyList<Tick> ticks, int windowSize)
    {
        var series = new List<Tick>(ticks.Count);
        if (ticks.Count == 0 || windowSize < 1) return series;

        var epoch = DateTime.UnixEpoch;
        for (var start = 0; start < ticks.Count; start += windowSize)
        {
            var end = Math.Min(start + windowSize, ticks.Count);
            var count = end - start;

            double sum = 0;
            for (var i = start; i < end; i++) sum += Mid(ticks[i]);
            var mean = sum / count;

            double sumSquares = 0;
            for (var i = start; i < end; i++)
            {
                var diff = Mid(ticks[i]) - mean;
                sumSquares += diff * diff;
            }
            var stdDev = Math.Sqrt(sumSquares / count);
            var isFlat = stdDev < FlatWindowEpsilon;

            for (var i = start; i < end; i++)
            {
                // A (near-)flat window has no meaningful direction to normalize toward --
                // z=0 for all of it rather than standardizing against a ~zero stdDev.
                var z = isFlat ? 0 : (Mid(ticks[i]) - mean) / stdDev;
                var syntheticTime = epoch + TimeSpan.FromTicks(TickUnit.Ticks * i);
                series.Add(new Tick(syntheticTime, z, z));
            }
        }
        return series;
    }

    private static double Mid(Tick tick) => (tick.Bid + tick.Ask) / 2.0;
}
