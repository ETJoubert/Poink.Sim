using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>One signal state to analyse: a catalog row's Above, In or Below bit.</summary>
internal readonly record struct SignalStateRef(SignalCatalogEntry Entry, SignalStateSlot Slot)
{
	public string Key =>
		Slot switch
		{
			SignalStateSlot.Above => Entry.AboveKey!,
			SignalStateSlot.Below => Entry.BelowKey!,
			_ => Entry.InKey,
		};

	public string Name =>
		$"{Entry.Label} {Slot switch { SignalStateSlot.Above => "▲", SignalStateSlot.Below => "▼", _ => "●" }}";

	public bool IsSet(SignalPointSeries series, int index) => (series.MaskFor(Slot)[index] & (1L << Entry.Index)) != 0;
}

/// <summary>
/// Measures what followed the entries into signal states -- the moments a state becomes true -- against the same
/// labels for every point, which is what entering at a random second would give.
/// </summary>
internal sealed class SignalStateAnalyzer
{
	private const double Z95 = 1.96;

	private readonly SignalPointSeries _series;
	private readonly SignalLabels _labels;
	private readonly SignalAnalysisOptions _options;
	private readonly double _pipSize;
	private readonly long _midTicks;
	private readonly long _spacingTicks;
	private double[] _baselineForward = Array.Empty<double>();

	public SignalStateAnalyzer(SignalPointSeries series, SignalLabels labels, SignalAnalysisOptions options, double pipSize)
	{
		_series = series;
		_labels = labels;
		_options = options;
		_pipSize = pipSize;
		_midTicks = series.Count > 0 ? series.Ticks[0] + (series.Ticks[^1] - series.Ticks[0]) / 2 : 0;
		_spacingTicks = options.NonOverlapping ? TimeSpan.FromMinutes(options.MaxMinutes).Ticks : 0;
	}

	public SignalStateStats Baseline()
	{
		var stats = Summarise("ALL POINTS", "all", null, Enumerable.Range(0, _series.Count));
		_baselineForward = stats.MeanForwardAtr.Select(mean => mean ?? 0).ToArray();
		return stats with { LiftAtr = stats.MeanForwardAtr.Select(_ => (double?)0).ToList() };
	}

	public SignalStateStats Single(SignalStateRef state) =>
		Summarise(state.Name, state.Key, state.Entry.Group, Entries(i => state.IsSet(_series, i)));

	public SignalStateStats Pair(SignalStateRef first, SignalStateRef second) =>
		Summarise(
			$"{first.Name} + {second.Name}",
			$"{first.Key}+{second.Key}",
			null,
			Entries(i => first.IsSet(_series, i) && second.IsSet(_series, i))
		);

	// An entry is the first point a state holds after a point where it did not; with non-overlapping on, entries
	// inside the previous counted entry's trade window are skipped.
	private IEnumerable<int> Entries(Func<int, bool> holds)
	{
		bool previous = false;
		long nextAllowed = long.MinValue;
		for (int i = 0; i < _series.Count; i++)
		{
			bool now = holds(i);
			if (now && !previous && _series.Ticks[i] >= nextAllowed)
			{
				nextAllowed = _series.Ticks[i] + _spacingTicks;
				yield return i;
			}
			previous = now;
		}
	}

	private SignalStateStats Summarise(string name, string key, RuleSignalGroup? group, IEnumerable<int> entries)
	{
		int horizons = _options.HorizonMinutes.Count;
		var forward = Enumerable.Range(0, horizons).Select(_ => new Accumulator()).ToArray();
		Accumulator longR = new(),
			shortR = new(),
			longFirst = new(),
			longSecond = new(),
			shortFirst = new(),
			shortSecond = new(),
			spread = new();
		int count = 0,
			longWins = 0,
			shortWins = 0;

		foreach (int i in entries)
		{
			count++;
			for (int h = 0; h < horizons; h++)
				forward[h].Add(_labels.ForwardAtr[h][i]);
			spread.Add((_series.Ask[i] - _series.Bid[i]) / _pipSize);

			bool firstHalf = _series.Ticks[i] < _midTicks;
			if (!double.IsNaN(_labels.LongR[i]))
			{
				longR.Add(_labels.LongR[i]);
				(firstHalf ? longFirst : longSecond).Add(_labels.LongR[i]);
				longWins += _labels.LongWin[i] ? 1 : 0;
			}
			if (!double.IsNaN(_labels.ShortR[i]))
			{
				shortR.Add(_labels.ShortR[i]);
				(firstHalf ? shortFirst : shortSecond).Add(_labels.ShortR[i]);
				shortWins += _labels.ShortWin[i] ? 1 : 0;
			}
		}

		var bestSide = (longR.Mean ?? double.MinValue) >= (shortR.Mean ?? double.MinValue) ? SignalTradeSide.Long : SignalTradeSide.Short;
		var best = bestSide == SignalTradeSide.Long ? longR : shortR;
		return new SignalStateStats(
			name,
			key,
			group,
			count,
			forward.Select(f => f.Mean).ToList(),
			forward.Select(f => f.HalfWidth(Z95)).ToList(),
			forward.Select((f, h) => f.Mean - (h < _baselineForward.Length ? _baselineForward[h] : 0)).ToList(),
			longR.Count > 0 ? (double)longWins / longR.Count : null,
			longR.Mean,
			shortR.Count > 0 ? (double)shortWins / shortR.Count : null,
			shortR.Mean,
			bestSide,
			best.Mean,
			best.TStat,
			bestSide == SignalTradeSide.Long ? longFirst.Mean : shortFirst.Mean,
			bestSide == SignalTradeSide.Long ? longSecond.Mean : shortSecond.Mean,
			spread.Mean
		);
	}

	private sealed class Accumulator
	{
		private double _sum;
		private double _sumOfSquares;

		public int Count { get; private set; }

		public double? Mean => Count > 0 ? _sum / Count : null;

		private double? StandardDeviation =>
			Count > 1 ? Math.Sqrt(Math.Max(0, (_sumOfSquares - _sum * _sum / Count) / (Count - 1))) : null;

		public double? HalfWidth(double z) => StandardDeviation is { } sd ? z * sd / Math.Sqrt(Count) : null;

		public double? TStat => Mean is { } mean && StandardDeviation is > 0 ? mean / (StandardDeviation.Value / Math.Sqrt(Count)) : null;

		public void Add(double value)
		{
			if (double.IsNaN(value))
				return;
			Count++;
			_sum += value;
			_sumOfSquares += value * value;
		}
	}
}
