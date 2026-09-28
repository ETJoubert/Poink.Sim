using System;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Incrementally folds a tick stream into fixed-width OHLC bars, mid-price (Bid+Ask)/2 per tick --
/// feeds the Live tab's chart from an episode's ticks as they're processed, one at a time, without
/// re-aggregating history on every call.
/// </summary>
internal sealed class LiveBarBuilder(int barWidthSeconds)
{
	private readonly long _barWidthTicks = Math.Max(1, barWidthSeconds) * TimeSpan.TicksPerSecond;
	private CTraderBarRow? _current;
	private long _bucketStartTicks;

	/// <summary>Folds one tick in, returning the (possibly just-started) in-progress bar and, only on the tick that rolls into a new bucket, the bar that just closed.</summary>
	public (CTraderBarRow Current, CTraderBarRow? JustClosed) Feed(Tick tick)
	{
		var price = (tick.Bid + tick.Ask) / 2.0;
		var bucketStartTicks = tick.Timestamp.Ticks - (tick.Timestamp.Ticks % _barWidthTicks);

		if (_current is null || bucketStartTicks != _bucketStartTicks)
		{
			var justClosed = _current;
			_bucketStartTicks = bucketStartTicks;
			_current = new CTraderBarRow(new DateTime(bucketStartTicks, tick.Timestamp.Kind), price, price, price, price, 1);
			return (_current, justClosed);
		}

		_current = _current with
		{
			High = Math.Max(_current.High, price),
			Low = Math.Min(_current.Low, price),
			Close = price,
			Volume = _current.Volume + 1,
		};
		return (_current, null);
	}
}
