// ── Percentage / Log Drift from current price series builder ────────────
//  Same single fixed-reference-point idea as TickDeltaTransform (the latest
//  tick's price anchors every earlier tick), but expressed as *relative*
//  drift from that anchor (p_t) rather than an absolute dollar difference:
//
//      Percentage: d_i = (p_i - p_t) / p_t
//      Log:        d_i = ln(p_i / p_t)
//
//  Why this matters: a 2% rally reads as the same drift value whether the
//  asset trades at $10 or $100 -- TickDeltaTransform's raw dollar difference
//  doesn't have that property, so a network trained on one price regime
//  wouldn't recognize the same move in another. Log drift additionally makes
//  a +2%/-2% move symmetric (ln(1.02) and ln(0.98) are equal in magnitude,
//  unlike the plain percentage form). See TransformerPage's Drift chart, the
//  only current caller.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Transformer;

internal enum DriftMode { Percentage, Log }

internal static class DriftFromCurrentPriceTransform
{
    // Same arbitrary synthetic spacing as TickDeltaTransform/LocalWindowZScoreTransform.
    private static readonly TimeSpan TickUnit = TimeSpan.FromSeconds(1);

    // ticks must already be in ascending chronological order (see
    // ITickRepository.ReadTicks/ReadRecentTicks). Unlike TickDeltaTransform's series
    // (which drops the anchor tick entirely), this returns one entry per input tick,
    // INCLUDING the anchor itself -- so the output is always the same length as the
    // input, with the last entry forced to exactly 0.0 (the anchor's drift from
    // itself). That fixed shape/zero-terminated convention matters if this ever feeds
    // a fixed-length lookback-window feature vector rather than just a chart.
    public static List<Tick> BuildDriftSeries(IReadOnlyList<Tick> ticks, DriftMode mode)
    {
        var series = new List<Tick>(ticks.Count);
        if (ticks.Count < 2) return series;

        var anchor = Mid(ticks[^1]);
        var epoch = DateTime.UnixEpoch;
        for (var k = 0; k < ticks.Count; k++)
        {
            var price = Mid(ticks[k]);
            var drift = mode == DriftMode.Log ? Math.Log(price / anchor) : (price - anchor) / anchor;
            var syntheticTime = epoch + TimeSpan.FromTicks(TickUnit.Ticks * k);
            series.Add(new Tick(syntheticTime, drift, drift));
        }

        // Guarantee numerical zero at the anchor's own point rather than trusting
        // floating-point division/log of identical values to land on exactly 0.0.
        var last = series[^1];
        series[^1] = last with { Bid = 0.0, Ask = 0.0 };

        return series;
    }

    private static double Mid(Tick tick) => (tick.Bid + tick.Ask) / 2.0;
}
