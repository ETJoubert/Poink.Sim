using System;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The synthetic asset's price history, as a close-price line or as OHLC bars, on the market scale it shares with the
	/// ladder, with the last price and the best bid and best ask drawn as level lines.
	/// </summary>
	public partial class EmulatorPriceChartComponent : ComponentBase
	{
		private const double ViewWidth = 1000;
		private const double ViewHeight = 400;

		[Parameter, EditorRequired]
		public EmulatorSnapshot Snapshot { get; set; } = default!;

		[Parameter]
		public double TickSize { get; set; } = 0.01;

		[Parameter, EditorRequired]
		public EmulatorPriceRange Range { get; set; }

		[Parameter]
		public EmulatorChartMode Mode { get; set; }

		[Parameter]
		public TimeSpan BarSpan { get; set; } = TimeSpan.FromMilliseconds(500);

		private string HistoryPoints { get; set; } = string.Empty;

		private (string Rising, string Falling) OhlcPaths { get; set; } = (string.Empty, string.Empty);

		private int BarCount { get; set; }

		private ChartLevel Price { get; set; } = default!;

		private ChartLevel? Bid { get; set; }

		private ChartLevel? Ask { get; set; }

		private string _priceFormat = "F2";

		protected override void OnParametersSet()
		{
			_priceFormat = EmulatorChartHelper.PriceFormat(TickSize);
			if (Mode == EmulatorChartMode.Ohlc)
			{
				var bars = EmulatorChartHelper.GroupBars(Snapshot.History, BarSpan);
				BarCount = bars.Count;
				OhlcPaths = EmulatorChartHelper.OhlcPaths(bars, Range, ViewWidth, ViewHeight);
			}
			else
			{
				HistoryPoints = EmulatorChartHelper.PolylinePoints(Snapshot.History, Range, ViewWidth, ViewHeight);
			}
			Price = LevelAt(Snapshot.Price);
			Bid = Snapshot.Book.BestBid is { } bid ? LevelAt(bid) : null;
			Ask = Snapshot.Book.BestAsk is { } ask ? LevelAt(ask) : null;
		}

		private ChartLevel LevelAt(double price)
		{
			var fraction = EmulatorChartHelper.YFraction(price, Range);
			return new ChartLevel(
				EmulatorChartHelper.Coord(fraction * ViewHeight),
				EmulatorChartHelper.Coord(fraction * 100),
				FormatPrice(price)
			);
		}

		private string FormatPrice(double price) => price.ToString(_priceFormat, CultureInfo.InvariantCulture);

		private sealed record ChartLevel(string Y, string TopPercent, string Label);
	}
}
