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

	public sealed record CTraderSymbolInfo(long SymbolId, string SymbolName, bool Enabled);

	public sealed record CTraderSymbolQuote(
		long SymbolId,
		string SymbolName,
		double? Bid,
		double? Ask,
		DateTime? UpdatedAt
	);

	public sealed record CTraderBar(DateTime TimeUtc, double Open, double High, double Low, double Close, long Volume);
}
