using System;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Simulation
{
	public partial class TraderControlsPanel : ComponentBase
	{
		[Inject]
		private ISimulationEngine Service { get; set; } = default!;

		[Parameter]
		public int? TraderId { get; set; }

		[Parameter]
		public string? PositionSummary { get; set; }

		[Parameter]
		public string? PendingSummary { get; set; }

		[Parameter]
		public double Cash { get; set; }

		[Parameter]
		public double Leverage { get; set; } = 1.0;

		[Parameter]
		public double StopOutLevelPercent { get; set; } = 50.0;

		[Parameter]
		public double PipSize { get; set; } = 0.01;

		[Parameter]
		public double? CurrentPrice { get; set; }

		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		private enum SizingMode
		{
			Pips,
			PercentCash,
		}

		private enum VolumeMode
		{
			Set,
			PercentCash,
		}

		private enum VolumeSizingMode
		{
			Manual,
			Risk,
		}

		private OrderType _orderType = OrderType.Market;
		private bool _isLong = true;
		private double _triggerPrice;
		private VolumeSizingMode _volumeSizingMode = VolumeSizingMode.Manual;
		private VolumeMode _volumeMode = VolumeMode.PercentCash;
		private double? _volumeUnits;
		private double _capitalPercent = 50;
		private double? _riskPercent;
		private SizingMode _stopLossMode = SizingMode.Pips;
		private double? _stopLossPips;
		private double? _stopLossPercent;
		private SizingMode _takeProfitMode = SizingMode.Pips;
		private double? _takeProfitPips;
		private double? _takeProfitPercent;
		private string? _error;

		private PositionDirection Direction => _isLong ? PositionDirection.Long : PositionDirection.Short;

		private double? MarketFillPrice => (_isLong ? CurrentAsk : CurrentBid) ?? CurrentPrice;

		private double? EntryReferencePrice =>
			_orderType == OrderType.Market ? MarketFillPrice
			: _triggerPrice > 0 ? _triggerPrice
			: null;

		private double CapitalFraction =>
			_volumeSizingMode == VolumeSizingMode.Risk
				? CalculatedCapitalFraction ?? 0
				: _volumeMode switch
				{
					VolumeMode.PercentCash => _capitalPercent / 100.0,
					VolumeMode.Set => EntryReferencePrice is { } entry
					&& entry > 0
					&& Cash > 0
					&& Leverage > 0
					&& _volumeUnits is { } units
						? units * entry / Leverage / Cash
						: 0,
					_ => 0,
				};

		/// <summary>
		/// Capital fraction solved from the stop-loss pip distance and the percent of cash risked.
		/// Null when any input is missing, or the required fraction exceeds 100% of cash.
		/// </summary>
		private double? CalculatedCapitalFraction
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				if (_stopLossPips is not { } pips || pips <= 0)
					return null;
				if (_riskPercent is not { } percent || percent <= 0)
					return null;
				if (Cash <= 0)
					return null;

				double stopPrice = RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize);
				double riskAmount = percent / 100.0 * Cash;
				return RiskHelper.CapitalFractionForRiskBounded(riskAmount, Cash, Leverage, entry, stopPrice);
			}
		}

		private bool HasStopLoss =>
			_stopLossMode == SizingMode.Pips ? _stopLossPips is not null : _stopLossPercent is not null;

		private double? StopLossPrice
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				return _stopLossMode == SizingMode.Pips
					? _stopLossPips is { } pips
						? RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize)
						: null
					: _stopLossPercent is { } percent
						? RiskHelper.StopLossPriceForRisk(
							Direction,
							Cash,
							CapitalFraction,
							Leverage,
							entry,
							percent / 100.0 * Cash
						)
						: null;
			}
		}

		private double? TakeProfitPrice
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				return _takeProfitMode == SizingMode.Pips
					? _takeProfitPips is { } pips
						? RiskHelper.TakeProfitPriceFromPips(Direction, entry, pips, PipSize)
						: null
					: _takeProfitPercent is { } percent
						? RiskHelper.TakeProfitPriceForReward(
							Direction,
							Cash,
							CapitalFraction,
							Leverage,
							entry,
							percent / 100.0 * Cash
						)
						: null;
			}
		}

		private double? Volume =>
			EntryReferencePrice is { } entry && entry > 0 ? RiskHelper.PositionSize(Cash, CapitalFraction, Leverage, entry) : null;

		private double MarginUsed => Math.Clamp(CapitalFraction, 0, 1) * Cash;

		private double? ProjectedLoss =>
			StopLossPrice is { } stop && EntryReferencePrice is { } entry
				? RiskHelper.ProjectedRiskOnStop(Direction, Cash, CapitalFraction, Leverage, entry, stop)
				: null;

		private double? ProjectedProfit =>
			TakeProfitPrice is { } target && EntryReferencePrice is { } entry && Volume is { } size
				? RiskHelper.RewardOnTarget(Direction, size, entry, target)
				: null;

		private double? ProjectedLossPercent =>
			ProjectedLoss is { } loss && Cash > 0 ? RiskHelper.RiskFraction(loss, Cash) : null;

		private double? ProjectedProfitPercent =>
			ProjectedProfit is { } profit && Cash > 0 ? RiskHelper.RiskFraction(profit, Cash) : null;

		private double? RewardToRisk =>
			ProjectedLoss is { } risk && ProjectedProfit is { } reward ? RiskHelper.RewardToRiskRatio(risk, reward) : null;

		private double? EntryDistancePips =>
			EntryReferencePrice is { } entry && MarketFillPrice is { } market
				? RiskHelper.PriceDistanceInPips(entry, market, PipSize)
				: null;

		private double? StopLossDistancePips =>
			StopLossPrice is { } stop && EntryReferencePrice is { } entry
				? RiskHelper.PriceDistanceInPips(entry, stop, PipSize)
				: null;

		private double? TakeProfitDistancePips =>
			TakeProfitPrice is { } target && EntryReferencePrice is { } entry
				? RiskHelper.PriceDistanceInPips(target, entry, PipSize)
				: null;

		private static string FormatPips(double? pips) => pips is { } value ? $"{value:N1}" : "--";

		private string MarginUsedText => $"{MarginUsed:N2}";

		/// <summary>
		/// Label, value, percent, row class and tooltip for the loss row -- a projected loss once a stop and
		/// entry are known, "--" while only the stop is set, or the margin stop-out ceiling when no stop is set.
		/// </summary>
		private (string Label, string Value, string Percent, string RowClass, string Title) LossRow
		{
			get
			{
				if (ProjectedLoss is { } loss)
				{
					string value = $"{(loss > 0 ? "-" : "+")}{Math.Abs(loss):N2}";
					string percent = ProjectedLossPercent is { } pct ? $"{Math.Abs(pct):P1}" : "--";
					return (
						"Loss",
						value,
						percent,
						loss > 0 ? "loss" : "gain",
						"Money lost if the stop is hit for this volume at this leverage from the entry price"
					);
				}
				if (HasStopLoss)
				{
					string title =
						_orderType == OrderType.Market
							? "A market price is needed to project the stop risk -- start playback"
							: "Enter a trigger price to project the stop risk";
					return ("Loss", "--", "--", "loss", title);
				}

				return (
					"Loss",
					"None",
					$"{StopOutLevelPercent:0.#}%",
					"loss",
					$"No stop set -- max loss before the {StopOutLevelPercent:0.#}% margin stop-out force-closes the position"
				);
			}
		}

		private string ProfitValueText => ProjectedProfit is { } profit ? $"+{Math.Abs(profit):N2}" : "--";

		private string ProfitPercentText => ProjectedProfitPercent is { } pct ? $"{Math.Abs(pct):P1}" : "--";

		private string RewardToRiskText => RewardToRisk is { } ratio ? $"{ratio:N2} : 1" : "--";

		private void SwitchToRiskSizing()
		{
			_volumeSizingMode = VolumeSizingMode.Risk;
			_stopLossMode = SizingMode.Pips;
		}

		private void SubmitEntry(bool isLong)
		{
			_error = null;
			if (TraderId is not { } id)
			{
				_error = "no trader selected";
				return;
			}
			if (CapitalFraction is <= 0 or > 1)
			{
				_error =
					_volumeSizingMode == VolumeSizingMode.Risk
						? "enter a stop loss and a percent of cash to risk so the size can be solved"
						: "volume must resolve to between 0 and 100% of cash";
				return;
			}
			if (_orderType != OrderType.Market && _triggerPrice <= 0)
			{
				_error = "enter a trigger price for a Limit/Stop order";
				return;
			}

			var action = isLong ? SimulationAction.EnterLong : SimulationAction.EnterShort;
			var orderType = _orderType == OrderType.Market ? (OrderType?)null : _orderType;
			var triggerPrice = _orderType == OrderType.Market ? (double?)null : _triggerPrice;
			var decision = new SimulationDecision(
				action,
				CapitalFraction,
				StopLossPrice,
				TakeProfitPrice,
				orderType,
				triggerPrice,
				Reason: "manual"
			);

			if (!Service.SubmitManualDecision(id, decision, out RiskPolicyDenial? denial))
				_error = "order rejected -- already in a position or an order is already resting";
			else if (denial is { } d)
				_error = $"denied by {d.PolicyName}: {d.Reason}";
		}

		private void SubmitCloseOrCancel()
		{
			_error = null;
			if (TraderId is not { } id)
			{
				_error = "no trader selected";
				return;
			}

			var decision = new SimulationDecision(SimulationAction.Close, Reason: "manual");
			if (Service.SubmitManualDecision(id, decision, out RiskPolicyDenial? denial) && denial is { } d)
				_error = $"denied by {d.PolicyName}: {d.Reason}";
		}
	}
}
