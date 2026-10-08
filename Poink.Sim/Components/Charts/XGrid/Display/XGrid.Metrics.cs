using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// XGrid's overlay and spectrum-highlight controls, rendered in its own header rows. Each setting stays a
	/// [Parameter] + *Changed callback so XGridController keeps owning the state it also merges with Indicators.
	/// </summary>
	public partial class XGrid
	{
		[Parameter]
		public EventCallback<XGridDisplaySettings> DisplayChanged { get; set; }

		[Parameter]
		public EventCallback<XGridSpectrumSettings> SpectrumChanged { get; set; }

		[Parameter]
		public EventCallback<XGridPeakOverlaySettings> LowPointOverlayChanged { get; set; }

		[Parameter]
		public EventCallback<XGridPeakOverlaySettings> HighPointOverlayChanged { get; set; }

		/// <summary>False when a host drives the level and time-line overlays from elsewhere (the Indicators component).</summary>
		[Parameter]
		public bool ShowOverlayRow { get; set; } = true;

		private Task SetDisplay(XGridDisplaySettings value) => DisplayChanged.InvokeAsync(Display = value);

		private XGridDisplaySettings WithoutTimeShading =>
			Display with { TimeWeighted = false, TimeCountWeighted = false, TimeCountSplit = false, TimeCountDistance = false };

		private XGridDisplaySettings WithoutShadingModes =>
			WithoutTimeShading with { ShadeDelta = false, DistanceWeighted = false };

		private Task ToggleShadeDelta() => SetDisplay(WithoutTimeShading with { ShadeDelta = !Display.ShadeDelta });

		private Task ToggleDistanceWeighted() =>
			SetDisplay(WithoutTimeShading with { DistanceWeighted = !Display.DistanceWeighted });

		private Task ToggleTimeWeighted() => SetDisplay(WithoutShadingModes with { TimeWeighted = !Display.TimeWeighted });

		private Task ToggleTimeCountWeighted() =>
			SetDisplay(WithoutShadingModes with { TimeCountWeighted = !Display.TimeCountWeighted });

		private Task ToggleTimeCountSplit() =>
			SetDisplay(
				WithoutShadingModes with
				{
					TimeCountWeighted = true,
					TimeCountSplit = !Display.TimeCountSplit,
					TimeCountDistance = Display.TimeCountDistance,
				}
			);

		private Task ToggleTimeCountDistance() =>
			SetDisplay(
				WithoutShadingModes with
				{
					TimeCountWeighted = true,
					TimeCountSplit = Display.TimeCountSplit,
					TimeCountDistance = !Display.TimeCountDistance,
				}
			);

		private Task ToggleDwellComparison() =>
			SetDisplay(
				Display with
				{
					DwellComparison = Display.DwellComparison == XGridDwellComparison.Above
						? XGridDwellComparison.Below
						: XGridDwellComparison.Above,
				}
			);

		private Task SetDwellSeconds(double value) => SetDisplay(Display with { DwellSeconds = Math.Max(0, value) });

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
