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
	}
}
