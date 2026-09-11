using System;

namespace Poink.Sim.Services.CTrader
{
	public enum CTraderConnectionState
	{
		Disconnected,
		Connecting,
		Connected,
		Failed,
	}

	public sealed record CTraderAccountInfo(long AccountId, long Login, bool IsLive);

	/// <summary>An open position, kept in sync via an initial reconcile plus live execution-event push.</summary>
	public sealed record CTraderPositionInfo(
		long PositionId,
		long SymbolId,
		string SymbolName,
		bool IsLong,
		double Volume,
		double EntryPrice,
		DateTime OpenTimeUtc,
		double? StopLossPrice,
		double? TakeProfitPrice,
		double UsedMargin
	);

	public sealed record CTraderSymbolInfo(
		long SymbolId,
		string SymbolName,
		bool Enabled,
		long BaseAssetId,
		long QuoteAssetId,
		long SymbolCategoryId,
		string Description,
		double SortingNumber
	);

	public sealed record CTraderSymbolQuote(
		long SymbolId,
		string SymbolName,
		double? Bid,
		double? Ask,
		DateTime? UpdatedAt
	);

	public sealed record CTraderBar(DateTime TimeUtc, double Open, double High, double Low, double Close, long Volume);

	public enum CTraderOrderType
	{
		Market,
		Limit,
		Stop,
	}

	/// <summary>Outcome of a PlaceOrderAsync/CloseSymbolAsync call, meant to be shown to the user as-is.</summary>
	public sealed record CTraderOrderResult(bool Success, string Message);

	/// <summary>A position-closing deal from account history, as returned by <see cref="ITradeHistoryAccess.GetClosedDealsAsync"/>.</summary>
	public sealed record CTraderDealInfo(
		long DealId,
		long PositionId,
		long SymbolId,
		string SymbolName,
		bool IsLong,
		double Volume,
		double ExecutionPrice,
		DateTime ExecutionTimeUtc,
		double? RealizedPnl,
		double? Commission,
		double? Swap
	);
}
