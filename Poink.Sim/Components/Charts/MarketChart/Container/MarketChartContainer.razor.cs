using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Composes MarketChartToolbar and MarketChart (the canvas) and owns the draw-mode/hint/expanded
	/// state that flows between them, so hosts only need to retarget the tag from &lt;MarketChart&gt;
	/// to &lt;MarketChartContainer&gt; -- the public parameter surface matches MarketChart's former one.
	/// </summary>
	public partial class MarketChartContainer : ComponentBase
	{
		[Parameter]
		public List<CTraderBarRow> Bars { get; set; } = new();

		/// <summary>Hides the Lines/Ranges/Shapes/Charts toolbar groups, leaving only Volume Range -- see ProfilerPage.</summary>
		[Parameter]
		public bool ShowAnnotationTools { get; set; } = true;

		/// <summary>False hides the toolbar entirely -- see MarketChart's own Interactive doc for why.</summary>
		[Parameter]
		public bool Interactive { get; set; } = true;

		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		[Parameter]
		public double? EntryPrice { get; set; }

		[Parameter]
		public bool? PositionIsLong { get; set; }

		[Parameter]
		public double? StopLossPrice { get; set; }

		[Parameter]
		public double? TakeProfitPrice { get; set; }

		[Parameter]
		public DateTime? WindowStartUtc { get; set; }

		[Parameter]
		public DateTime? WindowEndUtc { get; set; }

		[Parameter]
		public bool KeepView { get; set; }

		[Parameter]
		public bool LiveMode { get; set; }

		[Parameter]
		public EventCallback OnNearStart { get; set; }

		[Parameter]
		public IReadOnlyList<ChartHorizontalLine> HorizontalLines { get; set; } = Array.Empty<ChartHorizontalLine>();

		[Parameter]
		public IReadOnlyList<ChartVerticalLine> VerticalLines { get; set; } = Array.Empty<ChartVerticalLine>();

		[Parameter]
		public IReadOnlyList<ChartDiagonalLine> DiagonalLines { get; set; } = Array.Empty<ChartDiagonalLine>();

		[Parameter]
		public IReadOnlyList<ChartRangeFill> RangeFills { get; set; } = Array.Empty<ChartRangeFill>();

		[Parameter]
		public IReadOnlyList<ChartMarker> Markers { get; set; } = Array.Empty<ChartMarker>();

		[Parameter]
		public IReadOnlyList<ChartLinePoint> VwapLine { get; set; } = Array.Empty<ChartLinePoint>();

		[Parameter]
		public IReadOnlyList<ChartLinePoint> MedianLine { get; set; } = Array.Empty<ChartLinePoint>();

		[Parameter]
		public EventCallback<double> OnHorizontalLineDrawn { get; set; }

		[Parameter]
		public EventCallback<DateTime> OnVerticalLineDrawn { get; set; }

		[Parameter]
		public EventCallback<ChartDiagonalLine> OnDiagonalLineDrawn { get; set; }

		[Parameter]
		public EventCallback<ChartTimeRange> OnTRangeChartDrawn { get; set; }

		[Parameter]
		public EventCallback<DateTime> OnReferenceLineDrawn { get; set; }

		[Parameter]
		public EventCallback<ChartTimeRange> OnVolumeRangeDrawn { get; set; }

		/// <summary>True once the caller has a Volume Range selection -- drives the toolbar button's clear state.</summary>
		[Parameter]
		public bool HasVolumeRange { get; set; }

		[Parameter]
		public EventCallback OnVolumeRangeCleared { get; set; }

		[Parameter]
		public EventCallback OnDrawFinished { get; set; }

		private DrawMode _mode;
		private string? _drawHint;
		private bool _expanded;

		// Toggling the same mode again disarms it. The actual JS setDrawMode call happens inside MarketChart's
		// OnAfterRenderAsync, which diffs the Mode parameter the same way it diffs Bars/lines/markers/etc.
		private void ToggleMode(DrawMode mode) => _mode = _mode == mode ? DrawMode.None : mode;

		// Triples as arm/disarm/clear: armed -> disarm; unarmed with a selection -> clear via OnVolumeRangeCleared; otherwise arm.
		private async Task ToggleVolumeRange()
		{
			if (_mode == DrawMode.VolumeRange)
			{
				_mode = DrawMode.None;
				return;
			}

			if (HasVolumeRange)
			{
				if (OnVolumeRangeCleared.HasDelegate)
					await OnVolumeRangeCleared.InvokeAsync();
				return;
			}

			_mode = DrawMode.VolumeRange;
		}

		private void ToggleExpanded() => _expanded = !_expanded;

		private void SetMode(DrawMode mode) => _mode = mode;

		private void SetDrawHint(string? hint) => _drawHint = hint;
	}
}
