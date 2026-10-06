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
	/// Price ladder view of an order book on the market scale it shares with the chart, so each row sits level with its
	/// price on the chart: bids and asks shaded by their share of the deepest row, with the bots resting orders there and
	/// their equity alongside.
	/// </summary>
	public partial class EmulatorOrderBookComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public OrderBook Book { get; set; } = OrderBook.Empty;

		[Parameter, EditorRequired]
		public IReadOnlyList<PopulationLevel> Population { get; set; } = Array.Empty<PopulationLevel>();

		[Parameter, EditorRequired]
		public double Price { get; set; }

		[Parameter, EditorRequired]
		public EmulatorMarketScale Scale { get; set; } = default!;

		private IReadOnlyList<EmulatorLadderRow> _rows = Array.Empty<EmulatorLadderRow>();
		private double _maxVolume;
		private string _priceFormat = "F2";

		protected override void OnParametersSet()
		{
			_priceFormat = EmulatorChartHelper.PriceFormat(Scale.TickSize);
			_rows = EmulatorLadderHelper.BuildRows(Book, Population, Price, Scale);
			_maxVolume = _rows.Select(row => Math.Max(row.BidVolume ?? 0, row.AskVolume ?? 0)).DefaultIfEmpty(0).Max();
		}

		private static string RowClass(EmulatorLadderRow row) =>
			row.IsCurrentPrice ? "emu-ladder-current"
			: row.AskVolume is not null ? "emu-ladder-ask-row"
			: row.BidVolume is not null ? "emu-ladder-bid-row"
			: string.Empty;

		private string RowPrices(EmulatorLadderRow row) =>
			row.TopPrice > row.Price ? $"{FormatPrice(row.Price)}–{FormatPrice(row.TopPrice)}" : FormatPrice(row.Price);

		private string RowTooltip(EmulatorLadderRow row)
		{
			var price = RowPrices(row);
			return row.IsCurrentPrice ? $"Last traded price {FormatPrice(Price)}, in row {price}"
				: row.AskVolume is { } ask ? $"{FormatVolume(ask)} to sell at {price}"
				: row.BidVolume is { } bid ? $"{FormatVolume(bid)} to buy at {price}"
				: $"No orders resting at {price}";
		}

		private string PopulationTooltip(EmulatorLadderRow row) =>
			row.Population is { BotCount: > 0 } population
				? string.Create(
					CultureInfo.InvariantCulture,
					$"{population.BotCount:N0} bots with orders resting at {RowPrices(row)}, holding {population.Equity:N2} equity in total, {population.Equity / population.BotCount:N2} each on average"
				)
				: $"No bots with orders resting at {RowPrices(row)}";

		private string DepthWidth(double volume) =>
			EmulatorChartHelper.Coord(EmulatorChartHelper.DepthPercent(volume, _maxVolume));

		private string FormatPrice(double price) => price.ToString(_priceFormat, CultureInfo.InvariantCulture);

		private static string FormatVolume(double volume) => volume.ToString("N0", CultureInfo.InvariantCulture);
	}
}
