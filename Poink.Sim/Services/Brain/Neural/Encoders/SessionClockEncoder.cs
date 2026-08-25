using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class SessionClockEncoder : IObservationEncoder
{
	private const int Count = 4;
	private const double SecondsPerDay = 86400.0;
	private const double DaysPerWeek = 7.0;

	private readonly IFeatureNormalizer _normalizer;
	private readonly IReadOnlyList<FeatureDescriptor> _features;

	public SessionClockEncoder(IFeatureNormalizer? normalizer = null)
	{
		_normalizer = normalizer ?? new RawValueNormalizer();
		_features =
		[
			new FeatureDescriptor("session-tod-sin", _normalizer),
			new FeatureDescriptor("session-tod-cos", _normalizer),
			new FeatureDescriptor("session-dow-sin", _normalizer),
			new FeatureDescriptor("session-dow-cos", _normalizer),
		];
	}

	public int FeatureCount => Count;

	public int SequenceLength => 0;

	// Derived from the current tick's own timestamp; no history needed.
	public int WarmupBars => 0;

	public IReadOnlyList<FeatureDescriptor> Features => _features;

	public float[] Encode(SimulationObservation observation)
	{
		double timeOfDayAngle = observation.TimestampUtc.TimeOfDay.TotalSeconds / SecondsPerDay * 2 * Math.PI;
		double dayOfWeekAngle = (int)observation.TimestampUtc.DayOfWeek / DaysPerWeek * 2 * Math.PI;

		return
		[
			_normalizer.Normalize(Math.Sin(timeOfDayAngle), observation),
			_normalizer.Normalize(Math.Cos(timeOfDayAngle), observation),
			_normalizer.Normalize(Math.Sin(dayOfWeekAngle), observation),
			_normalizer.Normalize(Math.Cos(dayOfWeekAngle), observation),
		];
	}
}
