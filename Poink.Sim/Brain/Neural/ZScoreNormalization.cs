// ── Shared Local/Global Z-Score windowing math ───────────────────────────
//  Same algorithm as Transformer/LocalWindowZScoreTransform and
//  Transformer/GlobalWindowZScoreTransform (see those files' doc comments)
//  generalized to any ordered value series -- BarWindowEncoder applies it
//  across its trailing price window, VolumeProfileEncoder across a
//  histogram's bucket values. Global treats the whole series as one window
//  (one mean/stddev, applied to every entry); Local partitions it into
//  consecutive, non-overlapping windows of windowSize entries each,
//  normalized independently against their own mean/stddev -- same
//  FlatWindowEpsilon guard against dividing by a near-zero stdDev in a flat
//  window.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Brain.Neural;

internal static class ZScoreNormalization
{
    // Below this, a window is treated as flat (z=0 for all of it) rather
    // than standardized -- same guard/threshold as LocalWindowZScoreTransform/
    // GlobalWindowZScoreTransform.
    private const double FlatWindowEpsilon = 1e-8;

    public static float[] Global(IReadOnlyList<double> values) => Windowed(values, values.Count);

    public static float[] Local(IReadOnlyList<double> values, int windowSize) => Windowed(values, Math.Max(1, windowSize));

    private static float[] Windowed(IReadOnlyList<double> values, int windowSize)
    {
        var result = new float[values.Count];
        if (values.Count == 0 || windowSize < 1) return result;

        for (var start = 0; start < values.Count; start += windowSize)
        {
            var end = Math.Min(start + windowSize, values.Count);
            var count = end - start;

            double sum = 0;
            for (var i = start; i < end; i++) sum += values[i];
            var mean = sum / count;

            double sumSquares = 0;
            for (var i = start; i < end; i++)
            {
                var diff = values[i] - mean;
                sumSquares += diff * diff;
            }
            var stdDev = Math.Sqrt(sumSquares / count);
            var isFlat = stdDev < FlatWindowEpsilon;

            for (var i = start; i < end; i++)
                result[i] = isFlat ? 0f : (float)((values[i] - mean) / stdDev);
        }
        return result;
    }
}
