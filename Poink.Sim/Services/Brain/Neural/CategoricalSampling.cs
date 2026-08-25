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
				"policy produced non-finite probabilities (NaN/Inf) -- check input data and recent gradient updates for the cause"
			);

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
			if (cumulative >= threshold)
				return i;
		}
		// Float-rounding safety net: cumulative sum landed just under threshold.
		return count - 1;
	}

	public static int ArgmaxIndex(Tensor probabilities) => (int)probabilities.argmax(dim: 1).item<long>();
}
