using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.OrderBot
{
	internal sealed class OrderBotTradeExecution : IOrderBotTradeExecution
	{
		private readonly IOrderBotClient _client;
		private readonly OrderBotOptions _options;
		private readonly TimeProvider _timeProvider;
		private readonly ILogger<OrderBotTradeExecution> _logger;

		public OrderBotTradeExecution(
			IOrderBotClient client,
			IOptions<OrderBotOptions> options,
			TimeProvider timeProvider,
			ILogger<OrderBotTradeExecution> logger
		)
		{
			_client = client;
			_options = options.Value;
			_timeProvider = timeProvider;
			_logger = logger;
		}

		public Task<CTraderOrderResult> PlaceOrderAsync(
			long accountNumber,
			string symbolName,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		)
		{
			if (volumeUnits <= 0)
				return Task.FromResult(new CTraderOrderResult(false, "Volume must be greater than zero."));
			if (orderType != CTraderOrderType.Market && triggerPrice is not (> 0))
				return Task.FromResult(
					new CTraderOrderResult(false, "A trigger price is required for a Limit/Stop order.")
				);

			var command = NewCommand(accountNumber, OrderBotCommandKind.PlaceOrder) with
			{
				SymbolName = symbolName,
				IsLong = isLong,
				VolumeInUnits = volumeUnits,
				OrderType = ToBotOrderType(orderType),
				TriggerPrice = orderType == CTraderOrderType.Market ? null : triggerPrice,
				StopLossPrice = stopLossPrice,
				TakeProfitPrice = takeProfitPrice,
			};
			return SendAsync(command, cancellationToken);
		}

		public Task<CTraderOrderResult> ClosePositionAsync(
			long accountNumber,
			long positionId,
			CancellationToken cancellationToken = default
		) =>
			SendAsync(
				NewCommand(accountNumber, OrderBotCommandKind.ClosePosition) with
				{
					PositionId = positionId,
				},
				cancellationToken
			);

		public Task<CTraderOrderResult> CloseSymbolAsync(
			long accountNumber,
			string symbolName,
			CancellationToken cancellationToken = default
		) =>
			SendAsync(
				NewCommand(accountNumber, OrderBotCommandKind.CloseSymbol) with
				{
					SymbolName = symbolName,
				},
				cancellationToken
			);

		private OrderBotCommand NewCommand(long accountNumber, OrderBotCommandKind kind) =>
			new(
				Guid.NewGuid(),
				accountNumber,
				kind,
				_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(_options.CommandLifetimeSeconds)
			);

		private async Task<CTraderOrderResult> SendAsync(OrderBotCommand command, CancellationToken cancellationToken)
		{
			var result = await _client.SendAsync(command, cancellationToken);

			if (result.Success)
				_logger.LogInformation(
					"OrderBot: {Kind} command {CommandId} on account {AccountNumber} succeeded -- {Message}",
					command.Kind,
					command.CommandId,
					command.AccountNumber,
					result.Message
				);
			else
				_logger.LogWarning(
					"OrderBot: {Kind} command {CommandId} on account {AccountNumber} failed -- {Message}",
					command.Kind,
					command.CommandId,
					command.AccountNumber,
					result.Message
				);

			return new CTraderOrderResult(result.Success, result.Message);
		}

		private static OrderBotOrderType ToBotOrderType(CTraderOrderType orderType) =>
			orderType switch
			{
				CTraderOrderType.Limit => OrderBotOrderType.Limit,
				CTraderOrderType.Stop => OrderBotOrderType.Stop,
				_ => OrderBotOrderType.Market,
			};
	}
}
