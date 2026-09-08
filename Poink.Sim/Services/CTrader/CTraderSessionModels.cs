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

	/// <summary>
	/// Account-level financial snapshot for the Trade page header. Balance is fetched on connect and
	/// kept live via cTrader's ProtoOATraderUpdatedEvent push. The remaining figures would need to be
	/// derived from open positions (margin, unrealized P/L) and closed-deal history (realized P/L),
	/// which isn't implemented yet, so they stay null placeholders.
	/// </summary>
	public sealed record CTraderAccountFinancials(
		double? Balance,
		double? RealizedPnl,
		double? UnrealizedPnl,
		double? Equity,
		double? MarginUsed,
		double? MarginLevel,
		double? FreeMargin
	)
	{
		public static readonly CTraderAccountFinancials Empty = new(null, null, null, null, null, null, null);
	}

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
}
