using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Shared.Differentials;

namespace Poink.Sim.Helpers;

public static class DifferentialHelper
{
	public static DifferentialMeasure Measure(DifferentialPair pair, double pipSize)
	{
		double absolute = pair.To.Price - pair.From.Price;
		return new DifferentialMeasure(
			pair.Label,
			absolute,
			pipSize > 0 ? absolute / pipSize : 0,
			pair.From.Price != 0 ? absolute / pair.From.Price * 100 : 0,
			pair.From.Time is { } from && pair.To.Time is { } to ? to - from : null,
			[.. (pair.Metrics ?? []).Select(MeasureMetric)]
		);
	}

	private static DifferentialMetricMeasure MeasureMetric(DifferentialMetric metric)
	{
		double change = metric.To - metric.From;
		return new DifferentialMetricMeasure(
			metric.Label,
			change,
			metric.From != 0 ? change / Math.Abs(metric.From) * 100 : 0
		);
	}

	public static IReadOnlyList<DifferentialMeasure> MeasureAll(IEnumerable<DifferentialPair> pairs, double pipSize) =>
		[.. pairs.Select(p => Measure(p, pipSize))];
}
