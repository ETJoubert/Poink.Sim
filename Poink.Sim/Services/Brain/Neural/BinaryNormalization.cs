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
