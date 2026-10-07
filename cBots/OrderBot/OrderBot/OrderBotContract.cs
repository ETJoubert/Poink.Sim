using System;

namespace cAlgo.Robots;

// Wire contract shared with Poink.Sim; keep in step with Poink.Sim/Services/OrderBot/OrderBotModels.cs.
public enum BotCommandKind
{
	PlaceOrder,
	ClosePosition,
	CloseSymbol,
}

public enum BotOrderType
{
	Market,
	Limit,
	Stop,
}

public sealed record BotCommand(
	Guid CommandId,
	long AccountNumber,
	BotCommandKind Kind,
	DateTime ExpiresAtUtc,
	string SymbolName,
	bool IsLong,
	double VolumeInUnits,
	BotOrderType OrderType,
	double? TriggerPrice,
	double? StopLossPrice,
	double? TakeProfitPrice,
	long? PositionId
);

public sealed record BotCommandResult(bool Success, string Message, long? PositionId = null, long? OrderId = null);

public sealed record BotHealth(
	string BotName,
	long AccountNumber,
	bool IsLive,
	string BrokerName,
	bool AcceptingCommands
);
