using System;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Helpers;

/// <summary>Live pip/P&amp;L math for an open cTrader position against its watchlist quote and symbol metadata.</summary>
public static class PositionPnlHelper
{
	public static double PipSize(SymbolMetadataModel metadata) => 1.0 / Math.Pow(10, metadata.PipPosition);

	// Long P/L closes against the bid, short against the ask, matching how cTrader itself marks an open position.
	public static double? CurrentPrice(CTraderPositionInfo position, CTraderSymbolQuote? quote) =>
		position.IsLong ? quote?.Bid : quote?.Ask;

	public static double? PipDifference(CTraderPositionInfo position, CTraderSymbolQuote? quote, SymbolMetadataModel? metadata)
	{
		if (CurrentPrice(position, quote) is not { } price || metadata is null)
			return null;

		var priceDifference = position.IsLong ? price - position.EntryPrice : position.EntryPrice - price;
		return priceDifference / PipSize(metadata);
	}

	public static double? UnrealizedPnl(CTraderPositionInfo position, CTraderSymbolQuote? quote, SymbolMetadataModel? metadata) =>
		PipDifference(position, quote, metadata) is { } pips ? pips * position.Volume : null;
}
