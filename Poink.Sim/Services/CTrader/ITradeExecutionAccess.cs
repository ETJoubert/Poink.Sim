using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// The session slice trade-execution callers need: place/close orders and close positions.
	/// Implemented by the session, narrowed off <see cref="ICTraderSessionService"/> so callers can't
	/// reach watchlist or account/lifecycle members they have no business touching.
	/// </summary>
	internal interface ITradeExecutionAccess
	{
		bool UseLiveEnvironment { get; }

		/// <summary>
		/// Submits a new order for the given symbol. <paramref name="triggerPrice"/> is required for
		/// Limit/Stop and ignored for Market; <paramref name="volumeUnits"/> is in plain instrument units
		/// (converted to cTrader's 0.01-of-a-unit Volume field internally).
		/// </summary>
		Task<CTraderOrderResult> PlaceOrderAsync(
			long symbolId,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		);

		/// <summary>Closes every open position and cancels every resting pending order for the given symbol.</summary>
		Task<CTraderOrderResult> CloseSymbolAsync(long symbolId, CancellationToken cancellationToken = default);

		/// <summary>Closes a single open position by id, at its full current volume.</summary>
		Task<CTraderOrderResult> ClosePositionAsync(long positionId, CancellationToken cancellationToken = default);
	}
}
