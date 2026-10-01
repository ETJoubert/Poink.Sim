using System;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>Turns a trader's preconfigured control options into the volume, stop and target of each entry.</summary>
internal static class PositionControlHelper
{
	public static SimulationDecision BuildDecision(
		SimulationAction action,
		PositionControlOptionsModel control,
		double cash,
		double leverage,
		double pipSize,
		double bid,
		double ask
	)
	{
		if (action is not (SimulationAction.EnterLong or SimulationAction.EnterShort))
			return new SimulationDecision(action);

		var isLong = action == SimulationAction.EnterLong;
		var fillPrice = isLong ? ask : bid;
		var preview = Preview(control, cash, leverage, fillPrice, pipSize);
		if (preview.Volume <= 0)
			return new SimulationDecision(SimulationAction.Hold);

		return new SimulationDecision(
			action,
			CapitalFraction: Math.Clamp(preview.Margin / cash, 0, 1),
			StopLossPrice: control.HasStopLoss ? fillPrice + (isLong ? -preview.StopDistance : preview.StopDistance) : null,
			TakeProfitPrice: control.HasTakeProfit ? fillPrice + (isLong ? preview.TakeProfitDistance : -preview.TakeProfitDistance) : null
		);
	}

	public static PositionSizingPreview Preview(
		PositionControlOptionsModel control,
		double cash,
		double leverage,
		double price,
		double pipSize
	)
	{
		if (price <= 0 || cash <= 0 || pipSize <= 0)
			return default;

		var stopInPips = control.EffectiveStopLossUnit == StopLossUnit.Pips;
		var requested = control.SizingMode switch
		{
			PositionSizingMode.RiskPerTrade when control.HasStopLoss =>
				control.RiskPerTradePercent / 100.0 * cash / (control.StopLoss * pipSize),
			PositionSizingMode.RiskPerTrade => 0,
			_ => control.Volume,
		};
		var affordable = cash * Math.Max(leverage, 1) / price;
		var volume = Math.Max(0, Math.Min(requested, affordable));

		var loss = !control.HasStopLoss ? 0
			: stopInPips ? volume * control.StopLoss * pipSize
			: control.StopLoss / 100.0 * cash;
		var stopDistance = !control.HasStopLoss || volume <= 0 ? 0
			: stopInPips ? control.StopLoss * pipSize
			: loss / volume;
		var profit = control.HasTakeProfit ? control.TakeProfitBalancePercent / 100.0 * cash : 0;
		var takeProfitDistance = volume > 0 && control.HasTakeProfit ? profit / volume : 0;
		double? rewardRisk = loss > 0 && profit > 0 ? profit / loss : null;

		return new PositionSizingPreview(
			volume,
			volume * price,
			volume * price / Math.Max(leverage, 1),
			loss,
			profit,
			stopDistance,
			takeProfitDistance,
			rewardRisk,
			requested > affordable
		);
	}
}
