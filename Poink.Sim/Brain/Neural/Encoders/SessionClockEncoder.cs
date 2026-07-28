// ── Encoder: recurring session-clock signal ──────────────────────────────
//  Time-of-day and day-of-week, each as a sin/cos pair rather than a raw
//  fraction -- a cyclical quantity needs both components to stay unambiguous
//  across the wrap-around point (23:59 and 00:00 are a heartbeat apart in
//  real time, but a single fraction-of-day feature would put them at
//  opposite ends of [0,1]). FX session structure (Sydney/Tokyo/London/New
//  York open-close, weekend closure) is one of the strongest recurring
//  regularities in tick data, and this is the cheapest way to hand the
//  network what it needs to learn it.
//
//  Both derived straight from SimulationObservation.TimestampUtc -- no
//  history, no windowing, meaningful from the very first decision.
// ─────────────────────────────────────────────────────────────────────────
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
        // Already bounded [-1, 1] and unitless -- nothing to scale, same
        // passthrough treatment DirectionNormalizer gives a categorical flag.
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

    public int WarmupBars => 0; // derived from the current tick's own timestamp -- no history needed

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
