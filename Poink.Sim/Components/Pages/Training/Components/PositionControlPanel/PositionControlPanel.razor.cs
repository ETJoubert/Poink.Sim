using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Services.TrainingEngine;

namespace Poink.Sim.Components.Pages.Training.Components.PositionControlPanel
{
	public partial class PositionControlPanel : ComponentBase
	{
		[Parameter]
		public PositionControlOptionsModel Value { get; set; } = new();

		[Parameter]
		public EventCallback<PositionControlOptionsModel> ValueChanged { get; set; }

		[Parameter]
		public double StartingCash { get; set; } = 10_000;

		[Parameter]
		public double Leverage { get; set; } = 1;

		[Parameter]
		public double PipSize { get; set; } = 0.0001;

		[Parameter]
		public double? LatestPrice { get; set; }

		private PositionSizingPreview Preview => PositionControlHelper.Preview(Value, StartingCash, Leverage, LatestPrice ?? 0, PipSize);

		private static string ModeLabel(PositionSizingMode mode) =>
			mode == PositionSizingMode.RiskPerTrade ? "% Risk Per Trade" : "Fixed";

		private static string ModeTitle(PositionSizingMode mode) =>
			mode == PositionSizingMode.RiskPerTrade
				? "Derive volume so hitting the stop-loss loses a set percent of balance -- requires a stop-loss distance"
				: "Trade the same fixed number of units on every entry";

		private static string UnitLabel(StopLossUnit unit) => unit == StopLossUnit.Pips ? "Pips" : "% Balance";

		private static string UnitTitle(StopLossUnit unit) =>
			unit == StopLossUnit.Pips
				? "Stop-loss distance measured in pips of the selected symbol"
				: "Stop-loss placed where hitting it loses this percent of balance at the chosen volume -- unavailable when volume is derived from % risk";

		private string DistanceText()
		{
			if (LatestPrice is null || (!Value.HasStopLoss && !Value.HasTakeProfit))
				return "--";
			var stop = Value.HasStopLoss ? $"{Preview.StopDistance / PipSize:0.#}" : "--";
			var target = Value.HasTakeProfit ? $"{Preview.TakeProfitDistance / PipSize:0.#}" : "--";
			var rewardRisk = Preview.RewardRisk is { } rr ? $" ({rr:0.##}R)" : "";
			return $"{stop} / {target} pips{rewardRisk}";
		}

		private Task Update(PositionControlOptionsModel value) => ValueChanged.InvokeAsync(value);

		private double _volume;
		private double _riskPercent;
		private double _stopLoss;
		private double _takeProfitPercent;

		protected override void OnParametersSet()
		{
			_volume = Value.Volume;
			_riskPercent = Value.RiskPerTradePercent;
			_stopLoss = Value.StopLoss;
			_takeProfitPercent = Value.TakeProfitBalancePercent;
		}

		private Task Push() =>
			Update(Value with
			{
				Volume = _volume,
				RiskPerTradePercent = _riskPercent,
				StopLoss = _stopLoss,
				TakeProfitBalancePercent = _takeProfitPercent,
			});
	}
}
