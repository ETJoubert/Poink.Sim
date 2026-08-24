using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
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
		public bool IsManual { get; set; }

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

		private OrderType _orderType = OrderType.Market;
		private bool _isLong = true;
		private double _triggerPrice;
		private VolumeMode _volumeMode = VolumeMode.PercentCash;
		private double? _volumeUnits;
		private double _capitalPercent = 50;
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
			_volumeMode switch
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

		private void ToggleManualMode()
		{
			if (TraderId is not { } id)
				return;
			Service.SetTraderMode(id, IsManual ? TraderControlMode.Auto : TraderControlMode.Manual);
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
				_error = "volume must resolve to between 0 and 100% of cash";
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
