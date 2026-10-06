using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// One symbol's bars at one bar size: aggregated from its loaded history, then advanced by live ticks. Each update swaps
	/// in new lists rather than mutating, so a render holding the old ones never sees a half-applied tick and reference
	/// comparisons tell when bars changed. The raw ticks behind them (history plus every live tick since) are kept too,
	/// for consumers that need tick resolution up to the latest quote.
	/// </summary>
	internal sealed class TradeSymbolSeries
	{
		private readonly SimulationBarAggregator _aggregator;
		private readonly object _gate = new();
		private readonly List<Tick> _ticks = new();

		private TradeSymbolSeries(long symbolId, string symbolName, int barSizeSeconds, double pipSize, TickHistoryResult history)
		{
			SymbolId = symbolId;
			SymbolName = symbolName;
			BarSizeSeconds = barSizeSeconds;
			History = history;
			_aggregator = new SimulationBarAggregator(TimeSpan.FromSeconds(barSizeSeconds), pipSize);
		}

		public long SymbolId { get; }

		public string SymbolName { get; }

		public int BarSizeSeconds { get; }

		public TickHistoryResult History { get; }

		/// <summary>A List because the market chart takes one; never mutated once published.</summary>
		public List<CTraderBarRow> Bars { get; private set; } = new();

		public IReadOnlyList<HlocAvdtBar> HlocBars { get; private set; } = Array.Empty<HlocAvdtBar>();

		public IReadOnlyList<BarPriceHistogram> Histograms { get; private set; } = Array.Empty<BarPriceHistogram>();

		public static TradeSymbolSeries FromHistory(long symbolId, string symbolName, int barSizeSeconds, double pipSize, TickHistoryResult history)
		{
			var series = new TradeSymbolSeries(symbolId, symbolName, barSizeSeconds, pipSize, history);
			series.Seed(history.Ticks);
			return series;
		}

		public static CTraderBarRow ToBarRow(HlocAvdtBar bar) =>
			new(bar.OpenTime, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, bar.UpVolume, bar.DownVolume, bar.Distance);

		/// <summary>Timestamp of the oldest held raw tick, or null before any tick.</summary>
		public DateTime? FirstTickUtc
		{
			get
			{
				lock (_gate)
					return _ticks.Count > 0 ? _ticks[0].Timestamp : null;
			}
		}

		/// <summary>Timestamp of the newest held raw tick, or null before any tick.</summary>
		public DateTime? LastTickUtc
		{
			get
			{
				lock (_gate)
					return _ticks.Count > 0 ? _ticks[^1].Timestamp : null;
			}
		}

		/// <summary>A copy of the held raw ticks in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), oldest first.</summary>
		public List<Tick> TicksBetween(DateTime fromUtc, DateTime toUtc)
		{
			lock (_gate)
			{
				var start = LowerBound(fromUtc);
				var end = LowerBound(toUtc);
				return end > start ? _ticks.GetRange(start, end - start) : new List<Tick>();
			}
		}

		/// <summary>Feeds a live tick, finalizing the forming bar on a boundary crossing and updating or appending the new forming bar.</summary>
		public void Apply(Tick tick)
		{
			lock (_gate)
			{
				if (_ticks.Count == 0 || tick.Timestamp >= _ticks[^1].Timestamp)
					_ticks.Add(tick);

				var completed = _aggregator.Feed(tick);
				var bars = new List<CTraderBarRow>(Bars);
				var hlocBars = new List<HlocAvdtBar>(HlocBars);
				var histograms = new List<BarPriceHistogram>(Histograms);

				if (completed is { } c && hlocBars.Count > 0)
					Replace(bars, hlocBars, histograms, c.Bar, c.Histogram);

				if (_aggregator.CurrentPartialBar is { } partial)
				{
					var formingIsNew = completed is not null || hlocBars.Count == 0;
					if (formingIsNew)
						Append(bars, hlocBars, histograms, partial, _aggregator.CurrentPartialHistogram!);
					else
						Replace(bars, hlocBars, histograms, partial, _aggregator.CurrentPartialHistogram!);
				}

				Publish(bars, hlocBars, histograms);
			}
		}

		private void Seed(IReadOnlyList<Tick> ticks)
		{
			_ticks.AddRange(ticks);
			var bars = new List<CTraderBarRow>();
			var hlocBars = new List<HlocAvdtBar>();
			var histograms = new List<BarPriceHistogram>();
			foreach (var tick in ticks)
			{
				if (_aggregator.Feed(tick) is { } completed)
					Append(bars, hlocBars, histograms, completed.Bar, completed.Histogram);
			}
			if (_aggregator.CurrentPartialBar is { } partial)
				Append(bars, hlocBars, histograms, partial, _aggregator.CurrentPartialHistogram!);

			Publish(bars, hlocBars, histograms);
		}

		private int LowerBound(DateTime timeUtc)
		{
			int low = 0, high = _ticks.Count;
			while (low < high)
			{
				var mid = (low + high) / 2;
				if (_ticks[mid].Timestamp < timeUtc)
					low = mid + 1;
				else
					high = mid;
			}
			return low;
		}

		private void Publish(List<CTraderBarRow> bars, List<HlocAvdtBar> hlocBars, List<BarPriceHistogram> histograms)
		{
			Bars = bars;
			HlocBars = hlocBars;
			Histograms = histograms;
		}

		private static void Append(
			List<CTraderBarRow> bars,
			List<HlocAvdtBar> hlocBars,
			List<BarPriceHistogram> histograms,
			HlocAvdtBar bar,
			BarPriceHistogram histogram
		)
		{
			bars.Add(ToBarRow(bar));
			hlocBars.Add(bar);
			histograms.Add(histogram);
		}

		private static void Replace(
			List<CTraderBarRow> bars,
			List<HlocAvdtBar> hlocBars,
			List<BarPriceHistogram> histograms,
			HlocAvdtBar bar,
			BarPriceHistogram histogram
		)
		{
			bars[^1] = ToBarRow(bar);
			hlocBars[^1] = bar;
			histograms[^1] = histogram;
		}
	}
}
