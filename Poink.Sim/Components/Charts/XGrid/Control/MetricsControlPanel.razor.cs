using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// XGrid's measurement controls (pips/cols, overlays, spectrum highlights) extracted into their
	/// own component. Every value is exposed as a [Parameter] + *Changed callback, mirroring XGrid's
	/// existing PipCount/PipCountChanged pattern, so the panel self-manages sensible defaults when
	/// used standalone (unbound) and becomes fully driven when a host (XGridController) binds to it.
	/// </summary>
	public partial class MetricsControlPanel : ComponentBase
	{
		[Parameter]
		public int PipCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> PipCountChanged { get; set; }

		[Parameter]
		public int ColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> ColCountChanged { get; set; }

		/// <summary>Optional readouts (row height / column width) shown once a Grid is available.</summary>
		[Parameter]
		public XGridResult? Grid { get; set; }

		/// <summary>
		/// Host-supplied "Profile Bars" window size -- null when the host has no such concept. Only
		/// then does the Cols control offer a button to snap ColCount onto it.
		/// </summary>
		[Parameter]
		public int? ProfileBarCount { get; set; }

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

		private Task SetPipCount(int value) => PipCountChanged.InvokeAsync(PipCount = value);

		private Task SetColCount(int value) => ColCountChanged.InvokeAsync(ColCount = value);

		// While bound, Cols tracks ProfileBarCount on every parameter set (OnParametersSetAsync
		// below) instead of being independently editable.
		private bool _colsBoundToProfileBars;

		private Task ToggleColsBoundToProfileBars()
		{
			_colsBoundToProfileBars = !_colsBoundToProfileBars;
			return _colsBoundToProfileBars ? SyncColsToProfileBars() : Task.CompletedTask;
		}

		private Task SyncColsToProfileBars() =>
			ProfileBarCount is { } count && count != ColCount ? SetColCount(count) : Task.CompletedTask;

		protected override Task OnParametersSetAsync() =>
			_colsBoundToProfileBars ? SyncColsToProfileBars() : Task.CompletedTask;

		private Task SetDisplay(XGridDisplaySettings value) => DisplayChanged.InvokeAsync(Display = value);

		private Task SetSpectrum(XGridSpectrumSettings value) => SpectrumChanged.InvokeAsync(Spectrum = value);

		private Task SetLowPointOverlay(XGridPeakOverlaySettings value) =>
			LowPointOverlayChanged.InvokeAsync(LowPointOverlay = value);

		private Task SetHighPointOverlay(XGridPeakOverlaySettings value) =>
			HighPointOverlayChanged.InvokeAsync(HighPointOverlay = value);

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

		// All rows share one height by construction, so the first row's span stands for every row's.
		private static string RowHeightLabel(XGridResult grid) =>
			grid.RowLow.Count == 0
				? "--"
				: (grid.RowHigh[0] - grid.RowLow[0]).ToString("F5", CultureInfo.InvariantCulture);

		// Columns can each cover a different number of source bars, so this is the average spacing between column open times.
		private static string ColumnWidthLabel(XGridResult grid)
		{
			int cols = grid.Columns.Count;
			return cols < 2
				? "--"
				: FormatDuration((grid.Columns[^1].OpenTime - grid.Columns[0].OpenTime) / (cols - 1));
		}

		private static string FormatDuration(System.TimeSpan span)
		{
			if (span.TotalDays >= 1)
				return $"{(int)span.TotalDays}d {span.Hours}h";
			if (span.TotalHours >= 1)
				return $"{(int)span.TotalHours}h {span.Minutes}m";
			if (span.TotalMinutes >= 1)
				return $"{(int)span.TotalMinutes}m {span.Seconds}s";
			return $"{(int)span.TotalSeconds}s";
		}
	}
}
