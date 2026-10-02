using System;
using System.Collections.Generic;

namespace Poink.Sim.Components.Shared.Differentials;

/// <summary>A labelled price, optionally anchored in time, that a differential is measured from or to.</summary>
public readonly record struct DifferentialPoint(string Label, double Price, DateTime? Time = null);

/// <summary>A named quantity (ticks, delta...) taken at both ends of a pair, so its change can be measured too.</summary>
public readonly record struct DifferentialMetric(string Label, double From, double To);

/// <summary>Two points whose gap is to be measured, labelled for display, plus any extra quantities to compare.</summary>
public readonly record struct DifferentialPair(
	string Label,
	DifferentialPoint From,
	DifferentialPoint To,
	IReadOnlyList<DifferentialMetric>? Metrics = null
);

/// <summary>The change in one metric from a pair's From to its To; Percent is relative to From and 0 when From is 0.</summary>
public readonly record struct DifferentialMetricMeasure(string Label, double Absolute, double Percent);

/// <summary>
/// The gap from a pair's From to its To. Positive values mean To is above From. Duration is null
/// unless both points carry a time.
/// </summary>
public readonly record struct DifferentialMeasure(
	string Label,
	double Absolute,
	double Pips,
	double Percent,
	TimeSpan? Duration,
	IReadOnlyList<DifferentialMetricMeasure> Metrics
);
