using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Shared.PositionTable;
using Poink.Sim.Models;
using Poink.Sim.Simulation;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation page's Financials panel for one trader, fed straight from the simulation engine and re-rendered on
	/// its own, so balance, positions, history and statistics keep updating at max speed while the page does not render.
	/// </summary>
	public partial class SimulationFinancials : ComponentBase, IDisposable
	{
		[Inject]
		private ISimulationEngine Engine { get; set; } = default!;

		/// <summary>The trader to show; the first trader when unset or no longer present.</summary>
		[Parameter]
		public int? TraderId { get; set; }

		/// <summary>Raised when the open position's close button is pressed.</summary>
		[Parameter]
		public EventCallback OnClosePosition { get; set; }

		private sealed record DecisionLogRow(
			string Time,
			string Action,
			string Direction,
			string Size,
			string Price,
			double? RealizedPnl,
			double Commission,
			string Reason
		);

		private (IReadOnlyList<Trade>, double, double)? _statsKey;
		private TradeStatistics? _statsCache;

		private SimulationFinancialsState? State => Engine.Financials;

		private TraderSnapshot? Trader =>
			State is not { } state ? null : state.Traders.FirstOrDefault(t => t.Id == TraderId) ?? state.Traders.FirstOrDefault();

		protected override void OnInitialized() => Engine.OnFinancialsChanged += HandleFinancialsChanged;

		private void HandleFinancialsChanged() => _ = InvokeAsync(StateHasChanged);

		public void Dispose() => Engine.OnFinancialsChanged -= HandleFinancialsChanged;

		private static string FormatLogAction(string action) =>
			action switch
			{
				"EnterLong" => "BUY",
				"EnterShort" => "SHORT",
				"Close" => "CLOSE",
				_ => action,
			};

		private List<DecisionLogRow> LogRows =>
			Trader is not { } trader
				? new List<DecisionLogRow>()
				: trader
					.RecentLog.Select(l => new DecisionLogRow(
						l.TimestampUtc.ToString("HH:mm:ss"),
						FormatLogAction(l.Action),
						l.Direction.ToString(),
						l.Size.ToString("F4"),
						l.Price.ToString("F5"),
						l.RealizedPnl,
						l.Commission,
						l.Reason ?? ""
					))
					.Reverse()
					.ToList();

		private double? RealizedPnlPercentOfBalance() =>
			RiskHelper.PercentOfBalance(Trader?.Capital.RealizedPnl, Trader?.Capital.Cash);

		private TradeStatistics? Stats
		{
			get
			{
				if (Trader is not { } trader)
					return null;
				var key = (trader.Trades, trader.MaxEquity, trader.MaxDrawdown);
				if (_statsKey == key && _statsCache is not null)
					return _statsCache;

				_statsKey = key;
				_statsCache = TradeStatistics.From(trader.Trades, trader.MaxEquity, trader.MaxDrawdown);
				return _statsCache;
			}
		}

		private IReadOnlyList<StatGroup> StatGroups => Stats?.Groups ?? [];

		private IReadOnlyList<PositionRowViewModel<Position>> PositionRows =>
			State is { } state && Trader is { } trader && trader.Position is { } position
				? new[] { ToPositionRow(position, state, trader) }
				: Array.Empty<PositionRowViewModel<Position>>();

		private static PositionRowViewModel<Position> ToPositionRow(Position position, SimulationFinancialsState state, TraderSnapshot trader)
		{
			bool isLong = position.Direction == PositionDirection.Long;
			double pipSize = state.PipSize;
			double? currentPrice = (isLong ? state.Bid : state.Ask) ?? state.Price;
			double? pips =
				currentPrice is { } price && pipSize > 0
					? (isLong ? price - position.EntryPrice : position.EntryPrice - price) / pipSize
					: null;
			double? pnl = state.Bid is { } bid && state.Ask is { } ask ? position.UnrealizedPnl(bid, ask) : null;
			double? projectedProfit =
				position.TakeProfitPrice is { } tp
					? RiskHelper.RewardOnTarget(position.Direction, position.Size, position.EntryPrice, tp)
					: null;
			double? projectedLoss =
				position.StopLossPrice is { } sl
					? RiskHelper.RiskOnStop(position.Direction, position.Size, position.EntryPrice, sl)
					: null;
			double? rewardToRisk =
				projectedLoss is { } risk && projectedProfit is { } reward ? RiskHelper.RewardToRiskRatio(risk, reward) : null;
			double? percentToTakeProfit = ProgressTowardTarget(position.EntryPrice, position.TakeProfitPrice, pips, pipSize, favorable: true);
			double? percentToStopLoss = ProgressTowardTarget(position.EntryPrice, position.StopLossPrice, pips, pipSize, favorable: false);
			double? percentNet = RiskHelper.PercentOfBalance(pnl, trader.Capital.Cash);
			var duration = (state.TimestampUtc ?? DateTime.UtcNow) - position.EntryTime;

			return new PositionRowViewModel<Position>(
				position,
				state.Table,
				isLong,
				isLong ? "LONG" : "SHORT",
				position.Size.ToString("N2"),
				position.EntryPrice.ToString("N5"),
				pips,
				PositionRowFormat.PipsLabel(pips),
				pnl,
				PositionRowFormat.PnlLabel(pnl),
				position.EntryTime.ToString("HH:mm:ss"),
				position.StopLossPrice?.ToString("N5") ?? "—",
				position.TakeProfitPrice?.ToString("N5") ?? "—",
				projectedProfit,
				PositionRowFormat.MoneyLabel(projectedProfit),
				projectedLoss,
				PositionRowFormat.MoneyLabel(-projectedLoss),
				percentToTakeProfit,
				PositionRowFormat.PercentLabel(percentToTakeProfit),
				percentToStopLoss,
				PositionRowFormat.PercentLabel(percentToStopLoss),
				rewardToRisk,
				PositionRowFormat.RewardToRiskLabel(rewardToRisk),
				percentNet,
				PositionRowFormat.PercentLabel(percentNet),
				PositionRowFormat.DurationLabel(duration)
			);
		}

		/// <summary>Progress from entry toward a target price, as a percent of the entry-to-target pip distance.</summary>
		private static double? ProgressTowardTarget(double entryPrice, double? targetPrice, double? movedPips, double pipSize, bool favorable)
		{
			if (targetPrice is not { } target || pipSize <= 0 || movedPips is not { } moved)
				return null;
			double totalPips = Math.Abs(target - entryPrice) / pipSize;
			return totalPips <= 0 ? null : (favorable ? moved : -moved) / totalPips * 100;
		}
	}
}
