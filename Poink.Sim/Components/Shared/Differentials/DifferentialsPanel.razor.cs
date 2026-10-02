using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Shared.Differentials;

/// <summary>
/// Displays the gap between each supplied pair of values. It knows nothing about where the pairs
/// come from, so any chart or page can feed it.
/// </summary>
public partial class DifferentialsPanel : ComponentBase
{
	[Parameter]
	public string Title { get; set; } = "DIFFERENTIALS";

	[Parameter]
	public IReadOnlyList<DifferentialPair> Pairs { get; set; } = [];

	[Parameter]
	public double PipSize { get; set; } = 0.0001;

	[Parameter]
	public string EmptyMessage { get; set; } = "no values selected";

	private IReadOnlyList<DifferentialMeasure> _measures = [];

	private IReadOnlyList<string> _metricLabels = [];

	protected override void OnParametersSet()
	{
		_measures = DifferentialHelper.MeasureAll(Pairs, PipSize);
		_metricLabels = [.. _measures.SelectMany(m => m.Metrics).Select(x => x.Label).Distinct()];
	}

	private static DifferentialMetricMeasure? MetricOf(DifferentialMeasure measure, string label)
	{
		foreach (var metric in measure.Metrics)
			if (metric.Label == label)
				return metric;
		return null;
	}

	private static string Format(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

	private static string SignClass(double value) =>
		value > 0 ? "differentials-up"
		: value < 0 ? "differentials-down"
		: "";

	private static string DurationText(TimeSpan span)
	{
		TimeSpan abs = span.Duration();
		string text =
			abs.TotalDays >= 1 ? $"{(int)abs.TotalDays}d {abs.Hours}h"
			: abs.TotalHours >= 1 ? $"{(int)abs.TotalHours}h {abs.Minutes}m"
			: abs.TotalMinutes >= 1 ? $"{(int)abs.TotalMinutes}m {abs.Seconds}s"
			: $"{(int)abs.TotalSeconds}s";
		return span < TimeSpan.Zero ? "-" + text : text;
	}
}
