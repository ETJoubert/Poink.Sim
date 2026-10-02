using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// One trader's equity curve, fed straight from the simulation engine and re-rendered on its own, so it
	/// keeps updating at max speed while the rest of the Simulation page does not render.
	/// </summary>
	public partial class SimulationEquityCurve : ComponentBase, IDisposable
	{
		[Inject]
		private ISimulationEngine Engine { get; set; } = default!;

		[Parameter]
		public int? TraderId { get; set; }

		private IReadOnlyList<TradeEquityPoint>? _samplesSource;
		private IReadOnlyList<EquityCurveSample> _samples = Array.Empty<EquityCurveSample>();

		private IReadOnlyList<EquityCurveSample> Samples
		{
			get
			{
				var source =
					TraderId is { } id && Engine.TradeEquityByTrader.TryGetValue(id, out var points) ? points : null;
				if (ReferenceEquals(source, _samplesSource))
					return _samples;

				_samplesSource = source;
				_samples =
					source?.Select(p => new EquityCurveSample(p.TimestampUtc, p.Equity, p.MaxEquity, p.MinEquity, p.TradeNumber))
						.ToList()
					?? (IReadOnlyList<EquityCurveSample>)Array.Empty<EquityCurveSample>();
				return _samples;
			}
		}

		protected override void OnInitialized() => Engine.OnTradeEquityChanged += HandleTradeEquityChanged;

		private void HandleTradeEquityChanged() => _ = InvokeAsync(StateHasChanged);

		public void Dispose() => Engine.OnTradeEquityChanged -= HandleTradeEquityChanged;
	}
}
