using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>
	/// The app-wide <see cref="ITradeExecutionAccess"/>: a live account's trades go to the cTrader OrderBot bridge
	/// (the broker blocks Open API trading on live), everything else to the Open API session unchanged.
	/// </summary>
	internal sealed class TradeExecutionRouter : ITradeExecutionAccess
	{
		public const string OpenApiKey = "OpenApi";

		private readonly ITradeExecutionAccess _openApi;
		private readonly IOrderBotTradeExecution _bot;
		private readonly IAccountAccess _accounts;
		private readonly IWatchlistAccess _symbols;
		private readonly OrderBotOptions _options;

		public TradeExecutionRouter(
			[FromKeyedServices(OpenApiKey)] ITradeExecutionAccess openApi,
			IOrderBotTradeExecution bot,
			IAccountAccess accounts,
			IWatchlistAccess symbols,
			IOptions<OrderBotOptions> options
		)
		{
			_openApi = openApi;
			_bot = bot;
			_accounts = accounts;
			_symbols = symbols;
			_options = options.Value;
		}

		public bool UseLiveEnvironment => _openApi.UseLiveEnvironment;

		public Task<CTraderOrderResult> PlaceOrderAsync(
			long symbolId,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		)
		{
			if (!OrderBotRoutingHelper.TryGetBotAccount(_accounts, _options, out var account))
				return _openApi.PlaceOrderAsync(
					symbolId,
					isLong,
					volumeUnits,
					orderType,
					triggerPrice,
					stopLossPrice,
					takeProfitPrice,
					cancellationToken
				);

			return SymbolNameOf(symbolId) is { } symbolName
				? _bot.PlaceOrderAsync(
					account.Login,
					symbolName,
					isLong,
					volumeUnits,
					orderType,
					triggerPrice,
					stopLossPrice,
					takeProfitPrice,
					cancellationToken
				)
				: UnknownSymbol(symbolId);
		}

		public Task<CTraderOrderResult> CloseSymbolAsync(long symbolId, CancellationToken cancellationToken = default)
		{
			if (!OrderBotRoutingHelper.TryGetBotAccount(_accounts, _options, out var account))
				return _openApi.CloseSymbolAsync(symbolId, cancellationToken);

			return SymbolNameOf(symbolId) is { } symbolName
				? _bot.CloseSymbolAsync(account.Login, symbolName, cancellationToken)
				: UnknownSymbol(symbolId);
		}

		public Task<CTraderOrderResult> ClosePositionAsync(
			long positionId,
			CancellationToken cancellationToken = default
		) =>
			OrderBotRoutingHelper.TryGetBotAccount(_accounts, _options, out var account)
				? _bot.ClosePositionAsync(account.Login, positionId, cancellationToken)
				: _openApi.ClosePositionAsync(positionId, cancellationToken);

		private string? SymbolNameOf(long symbolId) =>
			_symbols.Symbols.FirstOrDefault(s => s.SymbolId == symbolId)?.SymbolName;

		private static Task<CTraderOrderResult> UnknownSymbol(long symbolId) =>
			Task.FromResult(new CTraderOrderResult(false, $"Symbol {symbolId} is not loaded for this account."));
	}
}
