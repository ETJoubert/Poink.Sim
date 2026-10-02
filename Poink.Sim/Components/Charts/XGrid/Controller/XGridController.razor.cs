using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Shared.Differentials;
using Poink.Sim.Components.Shared.Measurements;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Owns the overlay/highlight state MetricsControlPanel edits and XGrid renders, so the two never
	/// reference each other directly -- this is the "Card" that creates one instance of each per host
	/// (Profiler/Simulation/EpisodePlayback), replacing what used to be a single monolithic XGrid.
	/// PipCount/ColCount/Grid/order-panel/drawing parameters are pass-through, matching XGrid's former
	/// public surface so hosts only need to retarget the tag from &lt;XGrid&gt; to &lt;XGridController&gt;.
	/// </summary>
	public partial class XGridController : ComponentBase
	{
		[Parameter]
		public XGridResult? Grid { get; set; }

		[Parameter]
		public int PipCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> PipCountChanged { get; set; }

		[Parameter]
		public int ColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> ColCountChanged { get; set; }

		[Parameter]
		public int? ProfileBarCount { get; set; }

		[Parameter]
		public RenderFragment? ExpandedControls { get; set; }

		[Parameter]
		public IReadOnlyList<XGridLine>? Lines { get; set; }

		[Parameter]
		public EventCallback<XGridLine> OnLineDrawn { get; set; }

		[Parameter]
		public IReadOnlyList<XGridPosition>? Positions { get; set; }

		[Parameter]
		public EventCallback<XGridOrder> OnOrderPlaced { get; set; }

		[Parameter]
		public IReadOnlyList<XGridPending>? Pending { get; set; }

		[Parameter]
		public EventCallback<XGridOrderModification> OnPendingModified { get; set; }

		[Parameter]
		public EventCallback<string> OnPendingCancelled { get; set; }

		[Parameter]
		public EventCallback<string> OnPositionClosed { get; set; }

		[Parameter]
		public XGridExternalDraft? ExternalDraft { get; set; }

		[Parameter]
		public double PipSize { get; set; } = 0.0001;

		private XGridDisplaySettings _display = new();
		private XGridSpectrumSettings _spectrum = new();
		private XGridPeakOverlaySettings _lowPointOverlay = new();
		private XGridPeakOverlaySettings _highPointOverlay = new();

		private IReadOnlyList<XGridSelection> _selections = [];

		private IReadOnlyList<MeasurementColumn> MeasurementColumns =>
			Grid is { } grid ? XGridMeasurementHelper.MeasureAll(grid, _selections) : [];

		private IReadOnlyList<DifferentialPair> DifferentialPairs =>
			Grid is { } grid ? XGridDifferentialHelper.BuildPairs(grid, _selections) : [];

		private XGrid? _xGrid;

		private void RemoveSelection(int index) => _xGrid?.RemoveSelectionAt(index);

		private void SetSelections(IReadOnlyList<XGridSelection> value) => _selections = value;

		private Task SetPipCount(int value) => PipCountChanged.InvokeAsync(PipCount = value);

		private Task SetColCount(int value) => ColCountChanged.InvokeAsync(ColCount = value);

		private void SetDisplay(XGridDisplaySettings value) => _display = value;

		private void SetSpectrum(XGridSpectrumSettings value) => _spectrum = value;

		private void SetLowPointOverlay(XGridPeakOverlaySettings value) => _lowPointOverlay = value;

		private void SetHighPointOverlay(XGridPeakOverlaySettings value) => _highPointOverlay = value;
	}
}
