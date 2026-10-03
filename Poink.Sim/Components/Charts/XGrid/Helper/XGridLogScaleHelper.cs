using System;
using System.Collections.Generic;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Maps an integer value in [1, max] onto a linear slider position in [0, Steps] on a logarithmic
	/// curve, so low values occupy more of the track than high ones.
	/// </summary>
	public static class XGridLogScaleHelper
	{
		public const int Steps = 1000;

		public static int ValueToPosition(int value, int max) =>
			(int)Math.Round(Steps * Math.Log(Math.Clamp(value, 1, max)) / Math.Log(max));

		public static IReadOnlyList<int> OrderOfTenPositions(int max)
		{
			var positions = new List<int>();
			for (long value = 1; value <= max; value *= 10)
				positions.Add(ValueToPosition((int)value, max));
			return positions;
		}

		public static int PositionToValue(int position, int max) =>
			Math.Clamp((int)Math.Round(Math.Exp(Math.Clamp(position, 0, Steps) / (double)Steps * Math.Log(max))), 1, max);
	}
}
