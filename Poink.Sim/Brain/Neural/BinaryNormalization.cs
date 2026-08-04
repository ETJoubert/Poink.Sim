// ── Shared pairwise up/down binary encoding ──────────────────────────────
//  Sibling to ZScoreNormalization -- generalized to any ordered value series,
//  BarWindowEncoder applies it across its trailing price window,
//  VolumeProfileEncoder across a histogram's bucket values. Unlike Z-Score
//  there's no Local/Global split: each entry only ever compares against its
//  own immediate predecessor (n vs n-1), so no windowing/mean/stddev is
//  involved. 1 when the entry is higher than its predecessor, 0 when it's
//  lower or equal. The first entry has no predecessor within the series, so
//  it stays at its array-initialized neutral 0 -- same "no false signal"
//  treatment BarWindowEncoder's own zero-padding uses.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;

namespace Poink.Sim.Brain.Neural;

internal static class BinaryNormalization
{
    public static float[] Compute(IReadOnlyList<double> values)
    {
        var result = new float[values.Count];
        for (var i = 1; i < values.Count; i++)
            result[i] = values[i] > values[i - 1] ? 1f : 0f;
        return result;
    }
}
