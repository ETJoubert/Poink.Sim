using System;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Helpers;

/// <summary>
/// What a symbol allows a trade to be, in the same units the app trades in -- volume in units (cTrader's
/// metadata counts hundredths of a unit), SL/TP distances and prices in price terms.
/// </summary>
public sealed record SymbolTradingLimits(
	double PipSize,
	double MinVolume,
	double MaxVolume,
	double VolumeStep,
	double? MinStopLossDistancePrice,
	double? MinTakeProfitDistancePrice,
	double PriceStep,
	bool AllowShort
)
{
	private const double MetadataVolumeScale = 100.0;

	public static SymbolTradingLimits FromMetadata(SymbolMetadataModel metadata)
	{
		double pipSize = PositionPnlHelper.PipSize(metadata);
		return new SymbolTradingLimits(
			pipSize,
			metadata.MinVolume / MetadataVolumeScale,
			metadata.MaxVolume / MetadataVolumeScale,
			metadata.StepVolume / MetadataVolumeScale,
			PositionPnlHelper.MinDistancePips(metadata, metadata.SlDistance) is { } sl ? sl * pipSize : null,
			PositionPnlHelper.MinDistancePips(metadata, metadata.TpDistance) is { } tp ? tp * pipSize : null,
			1.0 / Math.Pow(10, metadata.Digits),
			metadata.EnableShortSelling
		);
	}

	/// <summary>Rounds a volume to the symbol's step and keeps it inside the symbol's minimum and maximum.</summary>
	public double NormalizeVolume(double volume)
	{
		double stepped = RiskHelper.RoundToStep(volume, VolumeStep);
		double atLeastMinimum = Math.Max(stepped, MinVolume);
		return MaxVolume > 0 ? Math.Min(atLeastMinimum, MaxVolume) : atLeastMinimum;
	}

	/// <summary>Pushes a stop-loss or take-profit price out to the minimum distance and onto the price step.</summary>
	public double NormalizeStopPrice(double entryPrice, double price, double? minDistancePrice) =>
		RiskHelper.RoundToStep(RiskHelper.ClampToMinimumDistance(entryPrice, price, minDistancePrice ?? 0), PriceStep);
}
