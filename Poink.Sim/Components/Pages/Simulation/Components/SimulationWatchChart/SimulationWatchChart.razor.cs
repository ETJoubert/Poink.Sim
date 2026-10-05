using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// One watched symbol's live chart on the Simulation page: its bars at the run's aggregation, or its recent ticks
	/// when the run is not aggregating, updated as the traded symbol plays.
	/// </summary>
	public partial class SimulationWatchChart : ComponentBase
	{
		[Parameter, EditorRequired]
		public string Table { get; set; } = "";

		[Parameter]
		public DateTime? CurrentTimestampUtc { get; set; }

		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		[Parameter]
		public bool Aggregate { get; set; }

		/// <summary>Keeps the user's pan/zoom across updates; off right after a load so the first bars fit the view.</summary>
		[Parameter]
		public bool KeepView { get; set; }

		[Parameter]
		public List<CTraderBarRow> Bars { get; set; } = new();

		/// <summary>Recent ticks as tick-table rows, newest first; shown instead of the chart when not aggregating.</summary>
		[Parameter]
		public List<object> TickRows { get; set; } = new();

		private double? CurrentPrice => CurrentBid is { } bid && CurrentAsk is { } ask ? (bid + ask) / 2.0 : null;
	}
}
