using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// The current-cell signal readout extracted from XGrid -- still rendered by XGrid itself
	/// (see XGrid.razor), just factored into its own component rather than inline markup.
	/// </summary>
	public partial class XGridSignalsPanel : ComponentBase
	{
		[Parameter]
		public XGridResult Grid { get; set; }

		[Parameter]
		public XGridSpectrumSettings Spectrum { get; set; } = new();

		[Parameter]
		public XGridPeakOverlaySettings LowPointOverlay { get; set; } = new();

		[Parameter]
		public XGridPeakOverlaySettings HighPointOverlay { get; set; } = new();

		// Reuses the matching Highlight overlay's percent/mode so the dot agrees with the canvas border.
		private bool CurrentSignal(XGridSpectrumMetric metric)
		{
			var (percent, mode) = metric switch
			{
				XGridSpectrumMetric.Up => (Spectrum.UpPercent, Spectrum.HighlightMode),
				XGridSpectrumMetric.Down => (Spectrum.DownPercent, Spectrum.HighlightMode),
				_ => (Spectrum.TotalPercent, Spectrum.HighlightMode),
			};
			return XGridStats.IsCurrentInTopPercent(Grid, metric, percent, mode, Spectrum.DistanceWeighted);
		}

		// Same reuse rationale as CurrentSignal, against whichever Delta overlay matches the sign.
		private bool CurrentDeltaSignal(bool positive) =>
			XGridStats.IsCurrentInDeltaPercent(
				Grid,
				positive ? Spectrum.DeltaPosPercent : Spectrum.DeltaNegPercent,
				positive,
				Spectrum.HighlightMode,
				Spectrum.DistanceWeighted
			);

		// Above/In/Below readout for one subject-vs-reference pair, mirroring XGridRuleCatalog's row shape.
		private readonly record struct PositionRow(
			string Label,
			string ColorVar,
			bool Above,
			bool In,
			bool Below,
			string AboveTooltip,
			string InTooltip,
			string BelowTooltip
		);

		private IReadOnlyList<PositionRow> PositionRows =>
			new[]
			{
				new PositionRow(
					"POC",
					XGridSignalCatalog.Get(XGridSignalCatalog.Keys.AtPoc).ColorVar,
					XGridStats.IsCurrentAbovePoc(Grid),
					XGridStats.IsCurrentAtPoc(Grid),
					XGridStats.IsCurrentBelowPoc(Grid),
					"Current price is above POC",
					"Current cell is at the POC row",
					"Current price is below POC"
				),
				new PositionRow(
					"VAH",
					XGridSignalCatalog.Get(XGridSignalCatalog.Keys.AtVah).ColorVar,
					XGridStats.IsCurrentAboveVah(Grid),
					XGridStats.IsCurrentAtVah(Grid),
					XGridStats.IsCurrentBelowVah(Grid),
					"Current price is above VAH",
					"Current cell is at the VAH row",
					"Current price is below VAH"
				),
				new PositionRow(
					"VAL",
					XGridSignalCatalog.Get(XGridSignalCatalog.Keys.AtVal).ColorVar,
					XGridStats.IsCurrentAboveVal(Grid),
					XGridStats.IsCurrentAtVal(Grid),
					XGridStats.IsCurrentBelowVal(Grid),
					"Current price is above VAL",
					"Current cell is at the VAL row",
					"Current price is below VAL"
				),
				new PositionRow(
					"VWAP",
					XGridSignalCatalog.Get(XGridSignalCatalog.Keys.AtVwap).ColorVar,
					XGridStats.IsCurrentAboveVwap(Grid),
					XGridStats.IsCurrentAtVwap(Grid),
					XGridStats.IsCurrentBelowVwap(Grid),
					"Current price is above VWAP",
					"Current cell is at the VWAP row",
					"Current price is below VWAP"
				),
				new PositionRow(
					"LOW",
					XGridSignalCatalog.Get("at-low").ColorVar,
					XGridStats.IsCurrentAboveLowLevel(Grid, LowPointOverlay.Settings),
					XGridStats.IsCurrentAtLowLevel(Grid, LowPointOverlay.Settings),
					XGridStats.IsCurrentBelowLowLevel(Grid, LowPointOverlay.Settings),
					"Current price is above the latest swing low",
					"Current cell is at the latest swing low's row",
					"Current price is below the latest swing low"
				),
				new PositionRow(
					"HIGH",
					XGridSignalCatalog.Get("at-high").ColorVar,
					XGridStats.IsCurrentAboveHighLevel(Grid, HighPointOverlay.Settings),
					XGridStats.IsCurrentAtHighLevel(Grid, HighPointOverlay.Settings),
					XGridStats.IsCurrentBelowHighLevel(Grid, HighPointOverlay.Settings),
					"Current price is above the latest swing high",
					"Current cell is at the latest swing high's row",
					"Current price is below the latest swing high"
				),
				new PositionRow(
					"VWAP/POC",
					XGridSignalCatalog.Get("vwap-at-poc").ColorVar,
					XGridStats.IsVwapAbovePoc(Grid),
					XGridStats.IsVwapAtPoc(Grid),
					XGridStats.IsVwapBelowPoc(Grid),
					"VWAP is above POC",
					"VWAP's row matches the POC row",
					"VWAP is below POC"
				),
				new PositionRow(
					"TOTAL/POC",
					XGridSignalCatalog.Get("total-at-poc").ColorVar,
					XGridStats.IsTotalAbovePoc(Grid),
					XGridStats.IsTotalAtPoc(Grid),
					XGridStats.IsTotalBelowPoc(Grid),
					"Total peak row is above POC",
					"Total peak row matches the POC row",
					"Total peak row is below POC"
				),
				new PositionRow(
					"UP/POC",
					XGridSignalCatalog.Get("up-at-poc").ColorVar,
					XGridStats.IsUpAbovePoc(Grid),
					XGridStats.IsUpAtPoc(Grid),
					XGridStats.IsUpBelowPoc(Grid),
					"Up peak row is above POC",
					"Up peak row matches the POC row",
					"Up peak row is below POC"
				),
				new PositionRow(
					"DOWN/POC",
					XGridSignalCatalog.Get("down-at-poc").ColorVar,
					XGridStats.IsDownAbovePoc(Grid),
					XGridStats.IsDownAtPoc(Grid),
					XGridStats.IsDownBelowPoc(Grid),
					"Down peak row is above POC",
					"Down peak row matches the POC row",
					"Down peak row is below POC"
				),
			};
	}
}
