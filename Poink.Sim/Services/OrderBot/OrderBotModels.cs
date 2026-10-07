using System;

namespace Poink.Sim.Services.OrderBot
{
	public enum OrderBotCommandKind
	{
		PlaceOrder,
		ClosePosition,
		CloseSymbol,
	}

	public enum OrderBotOrderType
	{
		Market,
		Limit,
		Stop,
	}

	/// <summary>
	/// Wire contract POSTed to the bot's /commands endpoint, mirrored by cBots/OrderBot/OrderBot/OrderBotContract.cs.
	/// <see cref="AccountNumber"/> is the trader login; the bot refuses a command addressed to any account but its own.
	/// </summary>
	public sealed record OrderBotCommand(
		Guid CommandId,
		long AccountNumber,
		OrderBotCommandKind Kind,
		DateTime ExpiresAtUtc,
		string? SymbolName = null,
		bool IsLong = false,
		double VolumeInUnits = 0,
		OrderBotOrderType OrderType = OrderBotOrderType.Market,
		double? TriggerPrice = null,
		double? StopLossPrice = null,
		double? TakeProfitPrice = null,
		long? PositionId = null
	);

	public sealed record OrderBotCommandResult(
		bool Success,
		string Message,
		long? PositionId = null,
		long? OrderId = null
	);

	/// <summary>The bot's GET /health reply: which account it is attached to and whether it is taking commands.</summary>
	public sealed record OrderBotHealth(
		string BotName,
		long AccountNumber,
		bool IsLive,
		string BrokerName,
		bool AcceptingCommands
	);

	/// <summary>A health probe's outcome: <see cref="Health"/> is null when the bot could not be reached or refused the probe.</summary>
	public sealed record OrderBotHealthResult(OrderBotHealth? Health, string Message);

	public enum OrderBotConnectionState
	{
		/// <summary>Not probing: live routing is off or the selected account is not live.</summary>
		Idle,
		Online,
		Offline,
	}

	public sealed record OrderBotStatus(OrderBotConnectionState State, string Message)
	{
		public static readonly OrderBotStatus Idle = new(OrderBotConnectionState.Idle, "No live account selected.");
	}
}
