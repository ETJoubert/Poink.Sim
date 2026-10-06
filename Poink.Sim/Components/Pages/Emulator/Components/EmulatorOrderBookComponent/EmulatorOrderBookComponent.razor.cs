using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// Price ladder view of an order book: the current price in the middle row, asks stacking up and bids stepping down,
	/// each level shaded by its share of the deepest level, with the bots there and their balance alongside.
	/// </summary>
	public partial class EmulatorOrderBookComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public OrderBook Book { get; set; } = OrderBook.Empty;

		[Parameter, EditorRequired]
		public IReadOnlyList<PopulationLevel> Population { get; set; } = Array.Empty<PopulationLevel>();

		[Parameter, EditorRequired]
		public double Price { get; set; }

		[Parameter]
		public double TickSize { get; set; } = 0.01;

		private IReadOnlyList<EmulatorLadderRow> _rows = Array.Empty<EmulatorLadderRow>();
		private double _maxVolume;
		private string _priceFormat = "F2";

		protected override void OnParametersSet()
		{
			_priceFormat = EmulatorChartHelper.PriceFormat(TickSize);
			_rows = EmulatorLadderHelper.BuildRows(Book, Population, Price, TickSize);
			_maxVolume = Book.Bids.Concat(Book.Asks).Select(level => level.Volume).DefaultIfEmpty(0).Max();
		}

		private static string RowClass(EmulatorLadderRow row) =>
			row.IsCurrentPrice ? "emu-ladder-current"
			: row.AskVolume is not null ? "emu-ladder-ask-row"
			: row.BidVolume is not null ? "emu-ladder-bid-row"
			: string.Empty;

		private string RowTooltip(EmulatorLadderRow row)
		{
			var price = FormatPrice(row.Price);
			return row.IsCurrentPrice ? $"Current price {price}"
				: row.AskVolume is { } ask ? $"{FormatVolume(ask)} to sell at {price}"
				: row.BidVolume is { } bid ? $"{FormatVolume(bid)} to buy at {price}"
				: $"No orders resting at {price}";
		}

		private string PopulationTooltip(EmulatorLadderRow row) =>
			row.Population is { BotCount: > 0 } population
				? string.Create(
					CultureInfo.InvariantCulture,
					$"{population.BotCount:N0} bots at {FormatPrice(row.Price)} holding {population.Balance:N2} in total, {population.Balance / population.BotCount:N2} each on average"
				)
				: $"No bots at {FormatPrice(row.Price)}";

		private string DepthWidth(double volume) =>
			EmulatorChartHelper.Coord(EmulatorChartHelper.DepthPercent(volume, _maxVolume));

		private string FormatPrice(double price) => price.ToString(_priceFormat, CultureInfo.InvariantCulture);

		private static string FormatVolume(double volume) => volume.ToString("N0", CultureInfo.InvariantCulture);
	}
}
