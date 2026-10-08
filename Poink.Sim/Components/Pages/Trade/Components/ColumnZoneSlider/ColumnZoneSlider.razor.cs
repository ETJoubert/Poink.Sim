using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// A horizontal gauge of where the current price sits along one zone axis (a column's price span, or a Time×Tk share),
	/// split into a LOW zone on the left, MID, and a HIGH zone on the right; caps either side light when the price is
	/// below or above what the axis covers.
	/// </summary>
	public partial class ColumnZoneSlider : ComponentBase
	{
		/// <summary>The axis's extent, price and place along it; null while there is nothing to read.</summary>
		[Parameter]
		public ColumnZoneReading? Reading { get; set; }

		/// <summary>Short row label, e.g. "n-2" or "Σ".</summary>
		[Parameter, EditorRequired]
		public string Label { get; set; } = null!;

		[Parameter, EditorRequired]
		public string LabelTooltip { get; set; } = null!;

		/// <summary>What the slider reads, as a noun phrase, e.g. "column n-2".</summary>
		[Parameter, EditorRequired]
		public string Subject { get; set; } = null!;

		/// <summary>What the axis spans, as a noun phrase, e.g. "column n-2's span".</summary>
		[Parameter, EditorRequired]
		public string Measure { get; set; } = null!;

		[Parameter, EditorRequired]
		public string PositionTooltip { get; set; } = null!;

		/// <summary>What the cells hold, for their tooltips, e.g. "Time×Tk".</summary>
		[Parameter]
		public string CellMeasure { get; set; } = "Time×Tk";

		/// <summary>Cells hold a signed value, shaded green above zero and red below.</summary>
		[Parameter]
		public bool CellsSigned { get; set; }

		/// <summary>Share of the axis the LOW and HIGH zones each cover.</summary>
		[Parameter]
		public double ZonePercent { get; set; } = ColumnZoneHelper.DefaultZonePercent;

		private ColumnZone? Zone => Reading?.Zone;

		private string BandActive(ColumnZone zone) => Zone == zone ? "active" : string.Empty;

		private string CellClass(ColumnZoneCell cell) =>
			!CellsSigned ? string.Empty
			: cell.Value < 0 ? "czs-cell-down"
			: "czs-cell-up";

		private string CellValueFormat => CellsSigned ? "+#,0;-#,0;0" : "N0";

		private string PositionText =>
			Reading switch
			{
				null => "--",
				{ Zone: ColumnZone.Below } => "below",
				{ Zone: ColumnZone.Above } => "above",
				{ } reading => $"{FormatPercent(reading.Position * 100)}%",
			};

		private string TrackTooltip =>
			Reading is { } reading
				? $"{Subject} {FormatPrice(reading.Low)} - {FormatPrice(reading.High)}; price {FormatPrice(reading.Price)} {(reading.IsOutside ? $"is {PositionText} it" : $"at {PositionText} of {Measure}")}"
				: $"Waiting for {Subject}";

		private static string CssPercent(double percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

		private static string FormatPercent(double percent) => percent.ToString("0", CultureInfo.InvariantCulture);

		private static string FormatPrice(double price) => price.ToString("0.#####", CultureInfo.InvariantCulture);
	}
}
