using System;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Helpers;

internal readonly record struct RobotEntryPlan(double CapitalFraction, double? StopLossPrice, double? TakeProfitPrice);

/// <summary>
/// Resolves the robot's price-independent <see cref="TradeConsoleOptions"/> into a concrete entry at a
/// given price and balance, using the same sizing math as TradeConsole's own preview.
/// </summary>
internal static class RobotPositionHelper
{
	private const double VolumeStep = 0.01;

	public static readonly TradeConsoleOptions DefaultOptions = new(
		true,
		VolumeSizingMode.Manual,
		VolumeStep,
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
		double pipSize
	)
	{
		if (entryPrice <= 0 || balance <= 0)
			return null;

		var direction = isLong ? PositionDirection.Long : PositionDirection.Short;
		if (ResolveVolume(options, direction, entryPrice, balance, pipSize) is not { } volume || volume <= 0)
			return null;

		double capitalFraction = RiskHelper.MarginRequired(volume, entryPrice, leverage) / balance;
		if (capitalFraction is <= 0 or > 1)
			return null;

		return new RobotEntryPlan(
			capitalFraction,
			StopLossPrice(options, direction, volume, entryPrice, balance, pipSize),
			TakeProfitPrice(options, direction, volume, entryPrice, balance, pipSize)
		);
	}

	private static double? ResolveVolume(
		TradeConsoleOptions options,
		PositionDirection direction,
		double entryPrice,
		double balance,
		double pipSize
	)
	{
		if (options.VolumeMode == VolumeSizingMode.Manual)
			return options.VolumeUnits;
		if (options.StopLossPips is not > 0 || options.RiskPercent is not > 0)
			return null;

		double stopPrice = RiskHelper.StopLossPriceFromPips(direction, entryPrice, options.StopLossPips.Value, pipSize);
		double riskAmount = options.RiskPercent.Value / 100.0 * balance;
		return RiskHelper.SizeForRiskRounded(riskAmount, entryPrice, stopPrice, VolumeStep);
	}

	private static double? StopLossPrice(
		TradeConsoleOptions options,
		PositionDirection direction,
		double volume,
		double entryPrice,
		double balance,
		double pipSize
	)
	{
		if (options.VolumeMode == VolumeSizingMode.Risk || options.StopLossMode == SizingMode.Pips)
			return options.StopLossPips is { } pips
				? RiskHelper.StopLossPriceFromPips(direction, entryPrice, pips, pipSize)
				: null;
		return options.StopLossPercent is { } percent
			? RiskHelper.StopLossPriceForSizeRisk(direction, volume, entryPrice, percent / 100.0 * balance)
			: null;
	}

	private static double? TakeProfitPrice(
		TradeConsoleOptions options,
		PositionDirection direction,
		double volume,
		double entryPrice,
		double balance,
		double pipSize
	)
	{
		if (options.TakeProfitMode == SizingMode.Pips)
			return options.TakeProfitPips is { } pips
				? RiskHelper.TakeProfitPriceFromPips(direction, entryPrice, pips, pipSize)
				: null;
		return options.TakeProfitPercent is { } percent
			? RiskHelper.TakeProfitPriceForSizeReward(direction, volume, entryPrice, percent / 100.0 * balance)
			: null;
	}
}
