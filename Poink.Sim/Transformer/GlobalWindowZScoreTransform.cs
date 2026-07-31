// ── Global (whole-window) Z-Score series builder ─────────────────────────
//  Counterpart to LocalWindowZScoreTransform's many small independently-
//  normalized sub-windows: this treats the ENTIRE input tick list as a single
//  lookback window -- one mean (mu_w) and standard deviation (sigma_w) over
//  every tick passed in, applied to every tick:
//
//      z_i = (p_i - mu_w) / sigma_w
//
//  This is the transform as originally specified ("normalize strictly inside
//  each input window using that window's own mean/stddev" -- one call, one
//  window, the whole thing). Whatever narrows the input (Volume Range
//  selection, MaxDeltaTicks) already defines "the window" -- there is no
//  further sub-division here, unlike LocalWindowZScoreTransform. See
//  TransformerPage's Global Z-Score chart, the only current caller, which
//  exists specifically to contrast against the local/sub-windowed version.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Transformer;

internal static class GlobalWindowZScoreTransform
{
    // Same arbitrary synthetic spacing as TickDeltaTransform -- SimulationBarAggregator
    // only cares about relative ordering/spacing, not real elapsed time.
    private static readonly TimeSpan TickUnit = TimeSpan.FromSeconds(1);

    // Below this, the whole window is treated as flat (every entry zeroed) rather than
    // standardized against a near-zero stdDev.
    private const double FlatWindowEpsilon = 1e-8;

    // ticks must already be in ascending chronological order (see
    // ITickRepository.ReadTicks/ReadRecentTicks). Returns one entry per input tick --
    // the whole list is the window, so (unlike TickDeltaTransform/
    // DriftFromCurrentPriceTransform) there's no separate "anchor" tick excluded or
    // forced to a particular value.
    public static List<Tick> BuildZScoreSeries(IReadOnlyList<Tick> ticks)
    {
        var series = new List<Tick>(ticks.Count);
        if (ticks.Count < 2) return series;

        double sum = 0;
        for (var i = 0; i < ticks.Count; i++) sum += Mid(ticks[i]);
        var mean = sum / ticks.Count;

        double sumSquares = 0;
        for (var i = 0; i < ticks.Count; i++)
        {
            var diff = Mid(ticks[i]) - mean;
            sumSquares += diff * diff;
        }
        var stdDev = Math.Sqrt(sumSquares / ticks.Count);
        var isFlat = stdDev < FlatWindowEpsilon;

        var epoch = DateTime.UnixEpoch;
        for (var i = 0; i < ticks.Count; i++)
        {
            var z = isFlat ? 0 : (Mid(ticks[i]) - mean) / stdDev;
            var syntheticTime = epoch + TimeSpan.FromTicks(TickUnit.Ticks * i);
            series.Add(new Tick(syntheticTime, z, z));
        }
        return series;
    }

    private static double Mid(Tick tick) => (tick.Bid + tick.Ask) / 2.0;
}
