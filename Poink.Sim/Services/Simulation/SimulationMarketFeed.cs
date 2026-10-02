using System;
using System.Collections.Generic;
using Poink.Sim.Bots;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

/// <summary>
/// The market a run's signals and bots read, built tick by tick: the most recent completed bars with their price
/// histograms, the bar still forming, and a running typical spread. Shared by the interactive simulation and the
/// headless signal-dataset builder, so both see exactly the same inputs.
/// </summary>
internal sealed class SimulationMarketFeed
{
	public const int MaxRecentBars = 300;
	private const int TypicalSpreadTicks = 1000;

	private readonly RingBuffer<HlocAvdtBar> _recentBars = new(MaxRecentBars);
	private readonly RingBuffer<BarPriceHistogram> _recentHistograms = new(MaxRecentBars);
	private SimulationBarAggregator? _aggregator;

	public RingBuffer<HlocAvdtBar> RecentBars => _recentBars;
	public RingBuffer<BarPriceHistogram> RecentHistograms => _recentHistograms;
	public SimulationBarAggregator? Aggregator => _aggregator;
	public bool IsAggregating => _aggregator is not null;
	public double? TypicalSpread { get; private set; }

	// One bucket per pip, so the XGrid's pip-multiple rows always cover whole buckets -- a coarser bucket would leave rows
	// narrower than it empty between populated ones.
	public static double BucketWidthFor(double pipSize) => pipSize;

	public void Reset(bool aggregate, TimeSpan barSize, double pipSize)
	{
		_recentBars.Clear();
		_recentHistograms.Clear();
		TypicalSpread = null;
		_aggregator = aggregate ? new SimulationBarAggregator(barSize, BucketWidthFor(pipSize)) : null;
	}

	/// <summary>Rebuilds the bars from <paramref name="ticks"/> at a new bar size, keeping the typical spread.</summary>
	public void Reaggregate(bool aggregate, TimeSpan barSize, double pipSize, IEnumerable<Tick> ticks)
	{
		_recentBars.Clear();
		_recentHistograms.Clear();
		_aggregator = aggregate ? new SimulationBarAggregator(barSize, BucketWidthFor(pipSize)) : null;
		if (_aggregator is null)
			return;
		foreach (var tick in ticks)
			AppendCompleted(_aggregator.Feed(tick));
	}

	public void Feed(Tick tick)
	{
		TrackTypicalSpread(tick.Ask - tick.Bid);
		if (_aggregator is not null)
			AppendCompleted(_aggregator.Feed(tick));
	}

	public BotTickContext ContextFor(Tick tick, SimulationConfig config) =>
		new(
			tick.Timestamp,
			(tick.Bid + tick.Ask) / 2.0,
			tick.Bid,
			tick.Ask,
			_recentBars,
			_recentHistograms,
			config.Table,
			config.LoadFrom,
			config.PipSize,
			TypicalSpread,
			config.BarSize
		);

	private void AppendCompleted((HlocAvdtBar Bar, BarPriceHistogram Histogram)? completed)
	{
		if (completed is not { } bar)
			return;
		_recentBars.Add(bar.Bar);
		_recentHistograms.Add(bar.Histogram);
	}

	private void TrackTypicalSpread(double spread)
	{
		const double weight = 2.0 / (TypicalSpreadTicks + 1);
		TypicalSpread = TypicalSpread is { } typical ? typical + weight * (spread - typical) : spread;
	}
}
