using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>Trade operations carried out by the cTrader OrderBot on the account identified by its trader login.</summary>
	internal interface IOrderBotTradeExecution
	{
		Task<CTraderOrderResult> PlaceOrderAsync(
			long accountNumber,
			string symbolName,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		);

		Task<CTraderOrderResult> ClosePositionAsync(
			long accountNumber,
			long positionId,
			CancellationToken cancellationToken = default
		);

		Task<CTraderOrderResult> CloseSymbolAsync(
			long accountNumber,
			string symbolName,
			CancellationToken cancellationToken = default
		);
	}
}
