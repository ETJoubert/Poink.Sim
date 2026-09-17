using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

// Buckets ticks into fixed-length time bars the same way SimulationBarAggregator does, but a
// completed bar reports the mid-price five-number summary (Min/Q1/Median/Q3/Max) instead of OHLC.
internal sealed class QuartileBarAggregator
{
	private readonly TimeSpan _barSize;
	private readonly List<double> _prices = new();

	private DateTime? _barStart;
	private DateTime _closeTime;

	public QuartileBarAggregator(TimeSpan barSize) => _barSize = barSize;

	public QuartileBar? CurrentPartialBar => _barStart is null || _prices.Count == 0 ? null : BuildBar();

	public QuartileBar? Feed(Tick tick)
	{
		var mid = (tick.Bid + tick.Ask) / 2.0;
		var thisBarStart = new DateTime(tick.Timestamp.Ticks - (tick.Timestamp.Ticks % _barSize.Ticks), tick.Timestamp.Kind);

		QuartileBar? completed = null;

		if (_barStart is null || thisBarStart != _barStart)
		{
			if (_barStart is not null && _prices.Count > 0)
				completed = BuildBar();

			_barStart = thisBarStart;
			_closeTime = tick.Timestamp;
			_prices.Clear();
			_prices.Add(mid);
		}
		else
		{
			_closeTime = tick.Timestamp;
			_prices.Add(mid);
		}

		return completed;
	}

	public void Reset()
	{
		_barStart = null;
		_prices.Clear();
	}

	private QuartileBar BuildBar()
	{
		_prices.Sort();
		return new QuartileBar(
			_barStart!.Value,
			_closeTime,
			_prices[0],
			Percentile(_prices, 0.25),
			Percentile(_prices, 0.5),
			Percentile(_prices, 0.75),
			_prices[^1],
			_prices.Count
		);
	}

	// Linear interpolation between closest ranks -- matches Excel's PERCENTILE.INC and NumPy's default method.
	private static double Percentile(List<double> sortedValues, double fraction)
	{
		if (sortedValues.Count == 1)
			return sortedValues[0];

		var rank = fraction * (sortedValues.Count - 1);
		var lowerIndex = (int)Math.Floor(rank);
		var upperIndex = (int)Math.Ceiling(rank);
		if (lowerIndex == upperIndex)
			return sortedValues[lowerIndex];

		var weight = rank - lowerIndex;
		return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
	}
}
