using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

internal static class DummyTickGenerator
{
	public static List<Tick> Generate(DateTime from, DateTime to, int count)
	{
		if (to <= from)
			to = from.AddHours(4);
		if (count < 2)
			count = 2;

		var interval = (to - from) / count;
		var rng = Random.Shared;
		double mid = 1.10000;
		const double spread = 0.00020;
		const double volatility = 0.00006;

		var ticks = new List<Tick>(count);
		for (int i = 0; i < count; i++)
		{
			mid = Math.Max(mid + (rng.NextDouble() - 0.5) * 2 * volatility, spread);
			ticks.Add(new Tick(from + interval * i, mid - spread / 2, mid + spread / 2));
		}
		return ticks;
	}
}
