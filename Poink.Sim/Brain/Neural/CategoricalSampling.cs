// ── Shared: categorical sampling over a small discrete probability vector ──
//  Extracted out of ComposedPolicyBrain's original SampleActionIndex so
//  DiscretizedLevelsActionHead's per-kind heads (Volume/StopLoss/
//  TakeProfit levels) can reuse the exact same native-crash-avoidance path
//  as the 4-way direction pick, instead of duplicating it. Sampling is done
//  manually (cumulative sum vs. one random draw) rather than via
//  TorchSharp's native `multinomial` -- that op has been observed to crash
//  the whole process on this exact call shape with two different native
//  exceptions (a CUDA "dtype Bool" error, then a CPU "NYI" from an internal
//  pow), both triggered by an ordinary near-one-hot probability vector
//  (e.g. a confident policy). Reading out a handful of floats and walking a
//  cumulative sum in managed code sidesteps that native op entirely --
//  cheap and exact for the small (<10-way) category counts every caller
//  here has.
// ─────────────────────────────────────────────────────────────────────────
using System;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Brain.Neural;

internal static class CategoricalSampling
{
    public static int SampleIndex(Tensor probabilities)
    {
        if (!isfinite(probabilities).all().item<bool>())
            throw new InvalidOperationException(
                "policy produced non-finite probabilities (NaN/Inf) -- check input data and recent gradient updates for the cause");

        int count = (int)probabilities.shape[1];
        Span<float> values = stackalloc float[count];
        float total = 0;
        for (int i = 0; i < count; i++)
        {
            values[i] = probabilities[0, i].item<float>();
            total += values[i];
        }

        float threshold = (float)Random.Shared.NextDouble() * total;
        float cumulative = 0;
        for (int i = 0; i < count; i++)
        {
            cumulative += values[i];
            if (cumulative >= threshold) return i;
        }
        return count - 1; // float-rounding safety net -- cumulative sum landed just under threshold
    }

    // DecisionDecodingKind.HardMax's deterministic counterpart to SampleIndex
    // above -- always the highest-probability index rather than a weighted
    // draw. Argmax over probabilities and over the pre-softmax logits agree
    // (softmax is monotonic), so callers with only one or the other on hand
    // can pass either. torch's own argmax is used directly here (unlike
    // SampleIndex's manual cumulative sum) since it's a plain reduction with
    // no reported crash risk, unlike multinomial.
    public static int ArgmaxIndex(Tensor probabilities) => (int)probabilities.argmax(dim: 1).item<long>();
}
