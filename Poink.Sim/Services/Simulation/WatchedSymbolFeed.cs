using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

/// <summary>
/// Replays one watch-only symbol in time lockstep with the traded symbol: each <see cref="AdvanceTo"/> consumes every
/// tick up to the traded symbol's current timestamp, building that symbol's own bars at the run's aggregation.
/// </summary>
internal sealed class WatchedSymbolFeed : IDisposable
{
	public const int MaxRecentTicks = 500;

	private readonly Func<IEnumerator<Tick>> _openReader;
	private readonly SimulationMarketFeed _market = new();
	private readonly RingBuffer<Tick> _recentTicks = new(MaxRecentTicks);
	private IEnumerator<Tick>? _reader;
	private Tick _pending;
	private bool _hasPending;
	private bool _exhausted;
	private Tick? _last;
	private IReadOnlyList<Tick> _publishedRecentTicks = Array.Empty<Tick>();
	private long _publishedRecentTicksVersion = -1;

	public WatchedSymbolFeed(SimulationWatchSymbol symbol, Func<IEnumerator<Tick>> openReader)
	{
		Symbol = symbol;
		_openReader = openReader;
	}

	public SimulationWatchSymbol Symbol { get; }

	/// <summary>Rewinds to the first tick of the range, with the bars rebuilt at this aggregation.</summary>
	public void Restart(bool aggregate, TimeSpan barSize)
	{
		_reader?.Dispose();
		_reader = _openReader();
		_hasPending = false;
		_exhausted = false;
		_last = null;
		_recentTicks.Clear();
		_market.Reset(aggregate, barSize, Symbol.PipSize);
	}

	/// <summary>Rebuilds the bars from the ticks already replayed, without moving the replay position.</summary>
	public void Reaggregate(bool aggregate, TimeSpan barSize) =>
		_market.Reaggregate(aggregate, barSize, Symbol.PipSize, _recentTicks);

	/// <summary>Consumes every tick stamped at or before <paramref name="timestampUtc"/>.</summary>
	public void AdvanceTo(DateTime timestampUtc)
	{
		while (TryPeek(out var tick) && tick.Timestamp <= timestampUtc)
		{
			_hasPending = false;
			_last = tick;
			_recentTicks.Add(tick);
			_market.Feed(tick);
		}
	}

	public WatchedSymbolSnapshot Snapshot()
	{
		var bars = _market.RecentBars.ToList(extraCapacity: 1);
		var histograms = _market.RecentHistograms.ToList(extraCapacity: 1);
		if (_market.Aggregator?.CurrentPartialBar is { } partial)
		{
			bars.Add(partial);
			histograms.Add(_market.Aggregator.CurrentPartialHistogram!);
		}

		if (_publishedRecentTicksVersion != _recentTicks.Version)
		{
			_publishedRecentTicks = _recentTicks.ToList();
			_publishedRecentTicksVersion = _recentTicks.Version;
		}

		return new WatchedSymbolSnapshot(
			Symbol.Table,
			_last?.Timestamp,
			_last?.Bid,
			_last?.Ask,
			bars,
			histograms,
			_publishedRecentTicks
		);
	}

	public void Dispose()
	{
		_reader?.Dispose();
		_reader = null;
	}

	private bool TryPeek(out Tick tick)
	{
		if (!_hasPending && !_exhausted && _reader is not null)
		{
			if (_reader.MoveNext())
			{
				_pending = _reader.Current;
				_hasPending = true;
			}
			else
				_exhausted = true;
		}
		tick = _pending;
		return _hasPending;
	}
}
