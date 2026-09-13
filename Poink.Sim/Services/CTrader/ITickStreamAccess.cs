using System;
using System.Collections.Generic;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// The session slice TradeChartPanel needs: live quotes/positions and the per-tick feed for
	/// aggregating bars. Implemented by the session, narrowed off <see cref="ICTraderSessionService"/>
	/// so the chart panel can't reach order placement or account/lifecycle members it has no business
	/// touching.
	/// </summary>
	internal interface ITickStreamAccess
	{
		CTraderConnectionState State { get; }

		/// <summary>Account balance, pushed live by cTrader.</summary>
		double? Balance { get; }

		/// <summary>Account leverage as a ratio (e.g. 50 for 1:50), pushed live by cTrader.</summary>
		double? Leverage { get; }

		IReadOnlyList<CTraderPositionInfo> Positions { get; }

		/// <summary>Every symbol with a live quote subscription -- the watchlist plus every open position's symbol.</summary>
		IReadOnlyList<CTraderSymbolQuote> Quotes { get; }

		/// <summary>Fires for every watched symbol's merged bid/ask quote.</summary>
		event Action<MarketTick>? TickReceived;
	}
}
