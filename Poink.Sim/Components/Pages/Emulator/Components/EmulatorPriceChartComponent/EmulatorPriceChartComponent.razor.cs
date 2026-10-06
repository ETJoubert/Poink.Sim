using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The synthetic asset's price history as a line, scaled around the current price so its level line stays at mid-height.
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

		private string PriceY { get; set; } = "0";

		private string PriceTopPercent { get; set; } = "50";

		private string _priceFormat = "F2";

		protected override void OnParametersSet()
		{
			_priceFormat = EmulatorChartHelper.PriceFormat(TickSize);
			Range = EmulatorChartHelper.RangeOf(Snapshot.History, Snapshot.Price, TickSize);
			HistoryPoints = EmulatorChartHelper.PolylinePoints(Snapshot.History, Range, ViewWidth, ViewHeight);
			var priceFraction = EmulatorChartHelper.YFraction(Snapshot.Price, Range);
			PriceY = EmulatorChartHelper.Coord(priceFraction * ViewHeight);
			PriceTopPercent = EmulatorChartHelper.Coord(priceFraction * 100);
		}

		private string FormatPrice(double price) => price.ToString(_priceFormat, CultureInfo.InvariantCulture);
	}
}
