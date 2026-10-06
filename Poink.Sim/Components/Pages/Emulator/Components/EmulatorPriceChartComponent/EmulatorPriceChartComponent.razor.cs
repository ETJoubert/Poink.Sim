using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The synthetic asset's price history as a line, scaled around the current price so its level line stays at mid-height,
	/// with the best bid and best ask drawn as the spread's edges.
	/// </summary>
	public partial class EmulatorPriceChartComponent : ComponentBase
	{
		private const double ViewWidth = 1000;
		private const double ViewHeight = 400;

		[Parameter, EditorRequired]
		public EmulatorSnapshot Snapshot { get; set; } = default!;

		[Parameter]
		public double TickSize { get; set; } = 0.01;

		private EmulatorPriceRange Range { get; set; }

		private string HistoryPoints { get; set; } = string.Empty;

		private ChartLevel Price { get; set; } = default!;

		private ChartLevel? Bid { get; set; }

		private ChartLevel? Ask { get; set; }

		private string _priceFormat = "F2";

		protected override void OnParametersSet()
		{
			_priceFormat = EmulatorChartHelper.PriceFormat(TickSize);
			Range = EmulatorChartHelper.RangeOf(
				Snapshot.History,
				Snapshot.Price,
				TickSize,
				Snapshot.Book.BestBid,
				Snapshot.Book.BestAsk
			);
			HistoryPoints = EmulatorChartHelper.PolylinePoints(Snapshot.History, Range, ViewWidth, ViewHeight);
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
