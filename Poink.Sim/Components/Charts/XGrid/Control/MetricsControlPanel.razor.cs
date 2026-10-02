using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// XGrid's overlay and spectrum-highlight controls, each a [Parameter] + *Changed callback.
	/// </summary>
	public partial class MetricsControlPanel : ComponentBase
	{
		[Parameter]
		public XGridDisplaySettings Display { get; set; } = new();

		[Parameter]
		public EventCallback<XGridDisplaySettings> DisplayChanged { get; set; }

		[Parameter]
		public XGridSpectrumSettings Spectrum { get; set; } = new();

		[Parameter]
		public EventCallback<XGridSpectrumSettings> SpectrumChanged { get; set; }

		[Parameter]
		public XGridPeakOverlaySettings LowPointOverlay { get; set; } = new();

		[Parameter]
		public EventCallback<XGridPeakOverlaySettings> LowPointOverlayChanged { get; set; }

		[Parameter]
		public XGridPeakOverlaySettings HighPointOverlay { get; set; } = new();

		[Parameter]
		public EventCallback<XGridPeakOverlaySettings> HighPointOverlayChanged { get; set; }

		private Task SetDisplay(XGridDisplaySettings value) => DisplayChanged.InvokeAsync(Display = value);

		private Task ToggleOverlaysEnabled() => SetDisplay(Display with { OverlaysEnabled = !Display.OverlaysEnabled });

		private Task ToggleHighlightsEnabled() =>
			SetSpectrum(Spectrum with { HighlightsEnabled = !Spectrum.HighlightsEnabled });

		// Stateless: OverlaysEnabled/HighlightsEnabled already remember what the master switch flips.
		private Task ToggleAllEnabled()
		{
			bool next = !(Display.OverlaysEnabled || Spectrum.HighlightsEnabled);
			return Task.WhenAll(
				SetDisplay(Display with { OverlaysEnabled = next }),
				SetSpectrum(Spectrum with { HighlightsEnabled = next })
			);
		}

		private Task SetSpectrum(XGridSpectrumSettings value) => SpectrumChanged.InvokeAsync(Spectrum = value);

		// Total/Up/Down/Delta+/Delta- share one percent threshold, so every field is written at once.
		private Task SetSharedPercent(double value) =>
			SetSpectrum(
				Spectrum with
				{
					TotalPercent = value,
					UpPercent = value,
					DownPercent = value,
					DeltaPosPercent = value,
					DeltaNegPercent = value,
				}
			);

		private Task SetLowPointOverlay(XGridPeakOverlaySettings value) =>
			LowPointOverlayChanged.InvokeAsync(LowPointOverlay = value);

		private Task SetHighPointOverlay(XGridPeakOverlaySettings value) =>
			HighPointOverlayChanged.InvokeAsync(HighPointOverlay = value);

		// Low/High share one Mode/Prominence/Distance/AdaptivePercent, so setting any of them writes
		// both overlays' copies at once -- only each direction's own Show toggle stays independent.
		private Task SetSharedMode(XGridPeakMode mode) =>
			Task.WhenAll(
				SetLowPointOverlay(LowPointOverlay with { Mode = mode }),
				SetHighPointOverlay(HighPointOverlay with { Mode = mode })
			);

		private Task SetSharedProminence(double value) =>
			Task.WhenAll(
				SetLowPointOverlay(LowPointOverlay with { MinProminence = value }),
				SetHighPointOverlay(HighPointOverlay with { MinProminence = value })
			);

		private Task SetSharedDistance(int value) =>
			Task.WhenAll(
				SetLowPointOverlay(LowPointOverlay with { MinDistance = value }),
				SetHighPointOverlay(HighPointOverlay with { MinDistance = value })
			);

		private Task SetSharedAdaptivePercent(double value) =>
			Task.WhenAll(
				SetLowPointOverlay(LowPointOverlay with { AdaptivePercent = value }),
				SetHighPointOverlay(HighPointOverlay with { AdaptivePercent = value })
			);

		private Task ToggleHighlightMode() =>
			SetSpectrum(
				Spectrum with
				{
					HighlightMode =
						Spectrum.HighlightMode == XGridHighlightMode.Rank
							? XGridHighlightMode.Max
							: XGridHighlightMode.Rank,
				}
			);

	}
}
