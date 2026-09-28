using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
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
		public bool HasSnapshot { get; set; }

		[Parameter]
		public DateTime? Timestamp { get; set; }

		[Parameter]
		public double Bid { get; set; }

		[Parameter]
		public double Ask { get; set; }

		[Parameter]
		public SimulationAction? LastAction { get; set; }

		[Parameter]
		public double Cash { get; set; }

		[Parameter]
		public double Equity { get; set; }

		[Parameter]
		public double RealizedPnl { get; set; }

		[Parameter]
		public double UnrealizedPnl { get; set; }

		[Parameter, EditorRequired]
		public string Table { get; set; } = "";

		[Parameter]
		public List<CTraderBarRow> Bars { get; set; } = [];

		[Parameter]
		public IReadOnlyList<EquityCurveSample> EquitySamples { get; set; } = [];

		[Parameter]
		public IReadOnlyList<FeatureDisplayRow> FeatureRows { get; set; } = [];

		private bool _showMarketChart = true;
		private bool _showXGrid;
		private bool _showTxFold = true;
		private bool _showEquityCurve = true;
		private bool _showFeatures = true;
	}
}
