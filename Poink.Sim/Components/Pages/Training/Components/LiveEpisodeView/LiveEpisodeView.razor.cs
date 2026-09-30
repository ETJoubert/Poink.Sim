using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Training
{
	/// <summary>Renders whichever panels are toggled on for the episode TrainPageV2's Run tab currently has in progress -- all data comes in as parameters, so it stays a pure display component.</summary>
	public partial class LiveEpisodeView : ComponentBase
	{
		[Parameter]
		public bool IsRunning { get; set; }

		[Parameter]
		public bool IsPaused { get; set; }

		[Parameter]
		public string? RunError { get; set; }

		[Parameter]
		public EventCallback OnPlayPause { get; set; }

		[Parameter]
		public EventCallback OnStep { get; set; }

		[Parameter]
		public EventCallback OnReset { get; set; }

		[Parameter]
		public string SpeedLabel { get; set; } = "1x";

		[Parameter]
		public bool CanDecreaseSpeed { get; set; }

		[Parameter]
		public bool CanIncreaseSpeed { get; set; }

		[Parameter]
		public EventCallback OnDecreaseSpeed { get; set; }

		[Parameter]
		public EventCallback OnIncreaseSpeed { get; set; }

		[Parameter]
		public EventCallback OnMaxSpeed { get; set; }

		[Parameter]
		public int EpisodesCompleted { get; set; }

		[Parameter]
		public int TargetEpisodeCount { get; set; }

		[Parameter]
		public bool HasSnapshot { get; set; }

		[Parameter]
		public DateTime? Timestamp { get; set; }

		[Parameter]
		public TimeSpan? TimePassed { get; set; }

		[Parameter]
		public int ActionCount { get; set; }

		[Parameter]
		public double Bid { get; set; }

		[Parameter]
		public double Ask { get; set; }

		[Parameter]
		public SimulationAction? LastAction { get; set; }

		[Parameter]
		public IReadOnlyList<RlOutputFeature>? ActionOutputs { get; set; }

		[Parameter]
		public double Cash { get; set; }

		[Parameter]
		public double Equity { get; set; }

		[Parameter]
		public double RealizedPnl { get; set; }

		[Parameter]
		public double UnrealizedPnl { get; set; }

		[Parameter]
		public int TradeCount { get; set; }

		[Parameter, EditorRequired]
		public string Table { get; set; } = "";

		[Parameter]
		public List<CTraderBarRow> Bars { get; set; } = [];

		[Parameter]
		public IReadOnlyList<EquityCurveSample> EquitySamples { get; set; } = [];

		[Parameter]
		public IReadOnlyList<FeatureDisplayRow> FeatureRows { get; set; } = [];

		/// <summary>TxFold's own grid-shaped rows, rendered separately from FeatureRows so it matches the Features tab preview's per-timeframe grid layout instead of a flat label/value list.</summary>
		[Parameter]
		public IReadOnlyList<TxFoldGridRow> TxFoldRows { get; set; } = [];

		// Bindable, not local state -- lets TrainPageV2 remember the panel selection across tab switches.
		[Parameter]
		public bool ShowMarketChart { get; set; } = true;

		[Parameter]
		public EventCallback<bool> ShowMarketChartChanged { get; set; }

		[Parameter]
		public bool ShowXGrid { get; set; }

		[Parameter]
		public EventCallback<bool> ShowXGridChanged { get; set; }

		[Parameter]
		public bool ShowTxFold { get; set; } = true;

		[Parameter]
		public EventCallback<bool> ShowTxFoldChanged { get; set; }

		[Parameter]
		public bool ShowEquityCurve { get; set; } = true;

		[Parameter]
		public EventCallback<bool> ShowEquityCurveChanged { get; set; }

		[Parameter]
		public bool ShowFeatures { get; set; } = true;

		[Parameter]
		public EventCallback<bool> ShowFeaturesChanged { get; set; }

		[Parameter]
		public bool ShowActions { get; set; } = true;

		[Parameter]
		public EventCallback<bool> ShowActionsChanged { get; set; }

		private Task TogglePanel(bool current, EventCallback<bool> changed) => changed.InvokeAsync(!current);

		private Task HideAllPanels() =>
			Task.WhenAll(
				ShowMarketChartChanged.InvokeAsync(false),
				ShowXGridChanged.InvokeAsync(false),
				ShowTxFoldChanged.InvokeAsync(false),
				ShowEquityCurveChanged.InvokeAsync(false),
				ShowFeaturesChanged.InvokeAsync(false),
				ShowActionsChanged.InvokeAsync(false)
			);

		private static string FormatTimePassed(TimeSpan? span) =>
			span switch
			{
				null => "-",
				{ TotalHours: >= 1 } s => $"{(int)s.TotalHours}h {s.Minutes}m",
				{ } s => $"{(int)s.TotalMinutes}m {s.Seconds}s",
			};
	}
}
