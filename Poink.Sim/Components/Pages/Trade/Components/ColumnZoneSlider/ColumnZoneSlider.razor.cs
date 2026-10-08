using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// A horizontal gauge of where the current price sits within one XGrid column: the column's span maps onto the bar's
	/// width, split into a LOW zone on the left, MID, and a HIGH zone on the right; caps either side light when the price
	/// is below or above the column.
	/// </summary>
	public partial class ColumnZoneSlider : ComponentBase
	{
		/// <summary>The column's span and current price; null while there is no such column to read.</summary>
		[Parameter]
		public ColumnZoneRange? Range { get; set; }

		/// <summary>Which column this slider reads, e.g. "n" or "n-2".</summary>
		[Parameter, EditorRequired]
		public string ColumnLabel { get; set; } = null!;

		/// <summary>Share of the span the LOW and HIGH zones each cover.</summary>
		[Parameter]
		public double ZonePercent { get; set; } = ColumnZoneHelper.DefaultZonePercent;

		private string ColumnName => $"column {ColumnLabel}";

		private double Position => Range is { } range ? ColumnZoneHelper.PricePosition(range) : 0;

		private ColumnZone? Zone => Range is { } range ? ColumnZoneHelper.ZoneOf(range, ZonePercent) : null;

		private bool IsOutside => Zone is ColumnZone.Below or ColumnZone.Above;

		private string BandActive(ColumnZone zone) => Zone == zone ? "active" : string.Empty;

		private string PositionText =>
			Zone switch
			{
				null => "--",
				ColumnZone.Below => "below",
				ColumnZone.Above => "above",
				_ => $"{FormatPercent(Position * 100)}%",
			};

		private string TrackTooltip =>
			Range is { } range
				? $"Column {ColumnLabel} {FormatPrice(range.Low)} - {FormatPrice(range.High)}; price {FormatPrice(range.Price)} {(IsOutside ? $"is {PositionText} it" : $"at {PositionText} of its span")}"
				: $"No XGrid {ColumnName} to read yet";

		private static string CssPercent(double percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

		private static string FormatPercent(double percent) => percent.ToString("0", CultureInfo.InvariantCulture);

		private static string FormatPrice(double price) => price.ToString("0.#####", CultureInfo.InvariantCulture);
	}
}
