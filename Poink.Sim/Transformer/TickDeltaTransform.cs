// ── "Difference from the latest tick" series builder ────────────────────
//  For the most recent tick n in a chronologically-ordered tick list, builds a
//  new synthetic tick series -- one entry per earlier tick k (k < n) -- whose
//  price is the difference between that earlier tick's own mid price and the
//  latest tick's mid price (mid(k) - mid(n)), and whose timestamp is a
//  synthetic, evenly-spaced one that preserves k's original chronological
//  order but replaces real (possibly irregular) elapsed time with a fixed
//  one-unit-per-tick spacing. That lets the existing time-bucketed
//  SimulationBarAggregator group the series by tick count ("N ticks per bar")
//  instead of by wall-clock time, independent of how the source ticks were
//  actually spaced -- see TransformerPage's tick-delta chart, the only
//  current caller.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Transformer;

internal static class TickDeltaTransform
{
    // Arbitrary synthetic spacing between consecutive entries -- SimulationBarAggregator
    // only cares about relative ordering/spacing, not real elapsed time, so any fixed
    // unit works; seconds just makes "N ticks per bar" a whole-number TimeSpan.
    private static readonly TimeSpan TickUnit = TimeSpan.FromSeconds(1);

    // ticks must already be in ascending chronological order (see
    // ITickRepository.ReadTicks/ReadRecentTicks). Returns one entry per tick strictly
    // before the last one -- the last tick (n) is the fixed reference point subtracted
    // from every earlier tick (k), not a data point of the series itself.
    public static List<Tick> BuildDeltaSeries(IReadOnlyList<Tick> ticks)
    {
        var series = new List<Tick>(Math.Max(0, ticks.Count - 1));
        if (ticks.Count < 2)
            return series;

        var latestMid = Mid(ticks[^1]);
        var epoch = DateTime.UnixEpoch;
        for (var k = 0; k < ticks.Count - 1; k++)
        {
            var delta = Mid(ticks[k]) - latestMid;
            var syntheticTime = epoch + TimeSpan.FromTicks(TickUnit.Ticks * k);
            series.Add(new Tick(syntheticTime, delta, delta));
        }
        return series;
    }

    private static double Mid(Tick tick) => (tick.Bid + tick.Ask) / 2.0;
}
