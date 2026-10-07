using System.Collections.Generic;
using System.Linq;

namespace cAlgo.Robots;

/// <summary>Carries out bridge commands on the cBot's main thread; prices in a command are absolute, as Poink.Sim sends them.</summary>
public partial class OrderBot
{
	private const string BridgeOrderLabel = "Poink.Sim";

	private BotCommandResult Execute(BotCommand command) =>
		command.Kind switch
		{
			BotCommandKind.PlaceOrder => PlaceOrder(command),
			BotCommandKind.ClosePosition => ClosePositionById(command),
			BotCommandKind.CloseSymbol => CloseSymbol(command),
			_ => Fail($"Unsupported command kind {command.Kind}."),
		};

	private BotCommandResult PlaceOrder(BotCommand command)
	{
		var symbol = Symbols.GetSymbol(command.SymbolName);
		if (symbol is null)
			return Fail($"Symbol '{command.SymbolName}' was not found on this account.");

		var volume = symbol.NormalizeVolumeInUnits(command.VolumeInUnits);
		if (volume < symbol.VolumeInUnitsMin)
			return Fail(
				$"Volume {command.VolumeInUnits} is below {symbol.Name}'s minimum of {symbol.VolumeInUnitsMin} units."
			);

		var side = command.IsLong ? TradeType.Buy : TradeType.Sell;
		var comment = command.CommandId.ToString("N");

		if (command.OrderType == BotOrderType.Market)
			return PlaceMarketOrder(symbol, side, volume, command, comment);
		if (command.TriggerPrice is not { } trigger)
			return Fail("A trigger price is required for a Limit/Stop order.");

		var result =
			command.OrderType == BotOrderType.Limit
				? PlaceLimitOrder(
					side,
					symbol.Name,
					volume,
					trigger,
					BridgeOrderLabel,
					command.StopLossPrice,
					command.TakeProfitPrice,
					ProtectionType.Absolute,
					null,
					comment
				)
				: PlaceStopOrder(
					side,
					symbol.Name,
					volume,
					trigger,
					BridgeOrderLabel,
					command.StopLossPrice,
					command.TakeProfitPrice,
					ProtectionType.Absolute,
					null,
					comment
				);

		return result.IsSuccessful
			? new BotCommandResult(
				true,
				$"{command.OrderType} order {result.PendingOrder.Id} placed: {side} {volume} {symbol.Name} at {trigger}.",
				OrderId: result.PendingOrder.Id
			)
			: Fail($"Rejected: {result.Error}");
	}

	// Market orders only take SL/TP as pip distances, so the position opens protected at approximately the requested
	// levels and is then pinned to the exact prices.
	private BotCommandResult PlaceMarketOrder(
		Symbol symbol,
		TradeType side,
		double volume,
		BotCommand command,
		string comment
	)
	{
		var reference = side == TradeType.Buy ? symbol.Ask : symbol.Bid;
		if (
			!TryPipDistance(
				symbol,
				side,
				reference,
				command.StopLossPrice,
				isStopLoss: true,
				out var stopLossPips,
				out var error
			)
			|| !TryPipDistance(
				symbol,
				side,
				reference,
				command.TakeProfitPrice,
				isStopLoss: false,
				out var takeProfitPips,
				out error
			)
		)
			return Fail(error);

		var result = ExecuteMarketOrder(
			side,
			symbol.Name,
			volume,
			BridgeOrderLabel,
			stopLossPips,
			takeProfitPips,
			comment
		);
		if (!result.IsSuccessful)
			return Fail($"Rejected: {result.Error}");

		var position = result.Position;
		var message = $"Filled: position {position.Id} {side} {volume} {symbol.Name} at {position.EntryPrice}.";
		if (command.StopLossPrice is null && command.TakeProfitPrice is null)
			return new BotCommandResult(true, message, position.Id);

		var modify = ModifyPosition(position, command.StopLossPrice, command.TakeProfitPrice, ProtectionType.Absolute);
		return modify.IsSuccessful
			? new BotCommandResult(true, message, position.Id)
			: new BotCommandResult(
				true,
				$"{message} SL/TP stayed at the pip-distance levels; pinning exact prices failed: {modify.Error}.",
				position.Id
			);
	}

	private static bool TryPipDistance(
		Symbol symbol,
		TradeType side,
		double reference,
		double? price,
		bool isStopLoss,
		out double? pips,
		out string error
	)
	{
		pips = null;
		error = null;
		if (price is not { } level)
			return true;

		var belowReference = level < reference;
		var mustBeBelow = (side == TradeType.Buy) == isStopLoss;
		if (belowReference != mustBeBelow)
		{
			error =
				$"{(isStopLoss ? "Stop loss" : "Take profit")} {level} is on the wrong side of the {side} price {reference}.";
			return false;
		}

		pips = Math.Abs(reference - level) / symbol.PipSize;
		return true;
	}

	private BotCommandResult ClosePositionById(BotCommand command)
	{
		var position = Positions.FirstOrDefault(p => p.Id == command.PositionId);
		if (position is null)
			return Fail($"Position {command.PositionId} is not open on this account.");

		var result = ClosePosition(position);
		return result.IsSuccessful
			? new BotCommandResult(true, $"Position {position.Id} closed.", position.Id)
			: Fail($"Close rejected: {result.Error}");
	}

	private BotCommandResult CloseSymbol(BotCommand command)
	{
		var positions = Positions.Where(p => p.SymbolName == command.SymbolName).ToList();
		var orders = PendingOrders.Where(o => o.SymbolName == command.SymbolName).ToList();
		if (positions.Count == 0 && orders.Count == 0)
			return Fail("Nothing open for this symbol.");

		var errors = new List<string>();
		foreach (var position in positions)
		{
			var result = ClosePosition(position);
			if (!result.IsSuccessful)
				errors.Add($"position {position.Id}: {result.Error}");
		}

		foreach (var order in orders)
		{
			var result = CancelPendingOrder(order);
			if (!result.IsSuccessful)
				errors.Add($"order {order.Id}: {result.Error}");
		}

		return errors.Count == 0
			? new BotCommandResult(true, $"Closed {positions.Count} position(s), cancelled {orders.Count} order(s).")
			: Fail(string.Join("; ", errors));
	}

	private static BotCommandResult Fail(string message) => new(false, message);
}
