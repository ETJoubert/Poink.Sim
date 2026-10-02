using System;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Helpers;

internal readonly record struct RobotEntryPlan(double CapitalFraction, double? StopLossPrice, double? TakeProfitPrice);

/// <summary>
/// Resolves the robot's price-independent <see cref="TradeConsoleOptions"/> into a concrete entry at a
/// given price and balance, using the same sizing math as TradeConsole's own preview and keeping the
/// result inside what the symbol allows.
/// </summary>
internal static class RobotPositionHelper
{
	public static readonly TradeConsoleOptions DefaultOptions = new(
		true,
		VolumeSizingMode.Manual,
		0.01,
		null,
		SizingMode.Pips,
		null,
		null,
		SizingMode.Pips,
		null,
		null
	);

	public static RobotEntryPlan? PlanEntry(
		TradeConsoleOptions options,
		bool isLong,
		double entryPrice,
		double balance,
		double leverage,
		SymbolTradingLimits limits
	)
	{
		if (entryPrice <= 0 || balance <= 0 || (!isLong && !limits.AllowShort))
			return null;

		var direction = isLong ? PositionDirection.Long : PositionDirection.Short;
		if (ResolveVolume(options, direction, entryPrice, balance, limits) is not { } rawVolume || rawVolume <= 0)
			return null;

		double volume = limits.NormalizeVolume(rawVolume);
		double capitalFraction = RiskHelper.MarginRequired(volume, entryPrice, leverage) / balance;
		if (capitalFraction is <= 0 or > 1)
			return null;

		return new RobotEntryPlan(
			capitalFraction,
			StopLossPrice(options, direction, volume, entryPrice, balance, limits),
			TakeProfitPrice(options, direction, volume, entryPrice, balance, limits)
		);
	}

	private static double? ResolveVolume(
		TradeConsoleOptions options,
		PositionDirection direction,
		double entryPrice,
		double balance,
		SymbolTradingLimits limits
	)
	{
		if (options.VolumeMode == VolumeSizingMode.Manual)
			return options.VolumeUnits;
		if (options.StopLossPips is not > 0 || options.RiskPercent is not > 0)
			return null;

		double stopPrice = RiskHelper.StopLossPriceFromPips(direction, entryPrice, options.StopLossPips.Value, limits.PipSize);
		double riskAmount = options.RiskPercent.Value / 100.0 * balance;
		return RiskHelper.SizeForRiskRounded(riskAmount, entryPrice, stopPrice, limits.VolumeStep);
	}

	private static double? StopLossPrice(
		TradeConsoleOptions options,
		PositionDirection direction,
		double volume,
		double entryPrice,
		double balance,
		SymbolTradingLimits limits
	)
	{
		double? price;
		if (options.VolumeMode == VolumeSizingMode.Risk || options.StopLossMode == SizingMode.Pips)
			price = options.StopLossPips is { } pips
				? RiskHelper.StopLossPriceFromPips(direction, entryPrice, pips, limits.PipSize)
				: null;
		else
			price = options.StopLossPercent is { } percent
				? RiskHelper.StopLossPriceForSizeRisk(direction, volume, entryPrice, percent / 100.0 * balance)
				: null;
		return price is { } p ? limits.NormalizeStopPrice(entryPrice, p, limits.MinStopLossDistancePrice) : null;
	}

	private static double? TakeProfitPrice(
		TradeConsoleOptions options,
		PositionDirection direction,
		double volume,
		double entryPrice,
		double balance,
		SymbolTradingLimits limits
	)
	{
		double? price;
		if (options.TakeProfitMode == SizingMode.Pips)
			price = options.TakeProfitPips is { } pips
				? RiskHelper.TakeProfitPriceFromPips(direction, entryPrice, pips, limits.PipSize)
				: null;
		else
			price = options.TakeProfitPercent is { } percent
				? RiskHelper.TakeProfitPriceForSizeReward(direction, volume, entryPrice, percent / 100.0 * balance)
				: null;
		return price is { } p ? limits.NormalizeStopPrice(entryPrice, p, limits.MinTakeProfitDistancePrice) : null;
	}
}
