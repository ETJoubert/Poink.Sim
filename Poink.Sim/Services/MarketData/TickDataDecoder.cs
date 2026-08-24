using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>One decoded quote-side sample: an absolute timestamp and price for a single bid or ask side.</summary>
	internal readonly record struct PricePoint(DateTime TimestampUtc, double Price);

	/// <summary>
	/// Decodes cTrader ProtoOAGetTickData responses (newest-first, delta-encoded: the first element is
	/// absolute, each subsequent element is the signed difference from the prior one) and merges a bid
	/// and an ask series into mid-quote ticks.
	/// </summary>
	internal static class TickDataDecoder
	{
		/// <summary>cTrader prices are integers scaled by 1/100_000 of a unit.</summary>
		private const double PriceScale = 100_000.0;

		public static List<PricePoint> Decode(IReadOnlyList<ProtoOATickData> tickData)
		{
			var points = new List<PricePoint>(tickData.Count);

			long timestampMs = 0,
				price = 0;
			for (var i = 0; i < tickData.Count; i++)
			{
				if (i == 0)
				{
					timestampMs = tickData[0].Timestamp;
					price = tickData[0].Tick;
				}
				else
				{
					timestampMs += tickData[i].Timestamp;
					price += tickData[i].Tick;
				}

				points.Add(
					new PricePoint(DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime, price / PriceScale)
				);
			}

			points.Reverse();
			return points;
		}

		/// <summary>
		/// Walks the combined bid/ask sample stream in time order, carrying the last-known other side, and
		/// emits a <see cref="Tick"/> once both sides are known -- the same merge the live stream performs.
		/// </summary>
		public static List<Tick> Merge(IReadOnlyList<PricePoint> bid, IReadOnlyList<PricePoint> ask)
		{
			var events = new List<(DateTime Ts, bool IsBid, double Price)>(bid.Count + ask.Count);
			foreach (var p in bid)
				events.Add((p.TimestampUtc, true, p.Price));
			foreach (var p in ask)
				events.Add((p.TimestampUtc, false, p.Price));
			events.Sort((a, b) => a.Ts.CompareTo(b.Ts));

			var ticks = new List<Tick>(events.Count);
			double? lastBid = null,
				lastAsk = null;
			foreach (var e in events)
			{
				if (e.IsBid)
					lastBid = e.Price;
				else
					lastAsk = e.Price;

				if (lastBid is { } b && lastAsk is { } a)
					ticks.Add(new Tick(e.Ts, b, a));
			}

			return ticks;
		}
	}
}
