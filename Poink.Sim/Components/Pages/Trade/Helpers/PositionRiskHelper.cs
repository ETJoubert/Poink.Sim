using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Pages.Trade.Helpers;

/// <summary>Projected TP/SL money and progress for an open cTrader position, built on <see cref="RiskHelper"/>.</summary>
public static class PositionRiskHelper
{
	public static double? ProjectedProfit(CTraderPositionInfo position, SymbolMetadataModel? metadata) =>
		metadata is not null && position.TakeProfitPrice is { } takeProfit
			? RiskHelper.RewardOnTarget(Direction(position), position.Volume, position.EntryPrice, takeProfit)
			: null;

	public static double? ProjectedLoss(CTraderPositionInfo position, SymbolMetadataModel? metadata) =>
		metadata is not null && position.StopLossPrice is { } stopLoss
			? RiskHelper.RiskOnStop(Direction(position), position.Volume, position.EntryPrice, stopLoss)
			: null;

	public static double? RewardToRisk(CTraderPositionInfo position, SymbolMetadataModel? metadata) =>
		ProjectedLoss(position, metadata) is { } risk && ProjectedProfit(position, metadata) is { } reward
			? RiskHelper.RewardToRiskRatio(risk, reward)
			: null;

	/// <summary>Progress from entry toward the take-profit, as a percent of the entry-to-TP pip distance.</summary>
	public static double? PercentToTakeProfit(CTraderPositionInfo position, CTraderSymbolQuote? quote, SymbolMetadataModel? metadata) =>
		Progress(position, quote, metadata, position.TakeProfitPrice, favorable: true);

	/// <summary>Progress from entry toward the stop-loss, as a percent of the entry-to-SL pip distance.</summary>
	public static double? PercentToStopLoss(CTraderPositionInfo position, CTraderSymbolQuote? quote, SymbolMetadataModel? metadata) =>
		Progress(position, quote, metadata, position.StopLossPrice, favorable: false);

	private static double? Progress(
		CTraderPositionInfo position,
		CTraderSymbolQuote? quote,
		SymbolMetadataModel? metadata,
		double? targetPrice,
		bool favorable
	)
	{
		if (metadata is null || targetPrice is not { } target)
			return null;
		if (PositionPnlHelper.PipDifference(position, quote, metadata) is not { } movedPips)
			return null;

		double totalPips = RiskHelper.PriceDistanceInPips(position.EntryPrice, target, PositionPnlHelper.PipSize(metadata));
		if (totalPips <= 0)
			return null;

		return (favorable ? movedPips : -movedPips) / totalPips * 100;
	}

	private static PositionDirection Direction(CTraderPositionInfo position) =>
		position.IsLong ? PositionDirection.Long : PositionDirection.Short;
}
