using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Helpers;

internal readonly record struct RobotEntryPlan(double CapitalFraction, double? StopLossPrice, double? TakeProfitPrice);

/// <summary>The volatility and value-area levels rule-based positioning measures stops and targets from; any may be unknown.</summary>
internal readonly record struct RobotMarketLevels(double? Atr, double? Poc, double? Vah, double? Val)
{
	public IEnumerable<double> Structure => new[] { Val, Poc, Vah }.Where(level => level is not null).Select(level => level!.Value);
}

/// <summary>
/// Resolves the robot's price-independent <see cref="TradeConsoleOptions"/> into a concrete entry at a
/// given price and balance, using the same sizing math as TradeConsole's own preview and keeping the
/// result inside what the symbol allows. <see cref="RobotPositionRules"/> can replace the console's stop and
/// target with ones measured from volatility or structure; risk sizing then follows that stop.
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
		RobotPositionRules rules,
		RobotMarketLevels levels,
		bool isLong,
		double entryPrice,
		double balance,
		double leverage,
		SymbolTradingLimits limits,
		double sizeMultiplier = 1
	)
	{
		if (entryPrice <= 0 || balance <= 0 || (!isLong && !limits.AllowShort))
			return null;

		var direction = isLong ? PositionDirection.Long : PositionDirection.Short;
		double? ruleStop = RuleStopPrice(rules, levels, direction, entryPrice, limits.PipSize);
		double? rawVolume = ruleStop is { } stop
			? VolumeForStop(options, entryPrice, stop, balance, limits)
			: ResolveVolume(options, direction, entryPrice, balance, limits);
		if (rawVolume is not > 0)
			return null;

		double volume = limits.NormalizeVolume(rawVolume.Value * Math.Clamp(sizeMultiplier, 0, 1));
		double capitalFraction = RiskHelper.MarginRequired(volume, entryPrice, leverage) / balance;
		if (capitalFraction is <= 0 or > 1)
			return null;

		double? stopLoss = ruleStop is { } s
			? limits.NormalizeStopPrice(entryPrice, s, limits.MinStopLossDistancePrice)
			: StopLossPrice(options, direction, volume, entryPrice, balance, limits);
		double? consoleTarget = TakeProfitPrice(options, direction, volume, entryPrice, balance, limits);
		double? takeProfit = rules.TargetMode == RobotTargetMode.Console
			? consoleTarget
			: RuleTargetPrice(rules, levels, direction, entryPrice, stopLoss) is { } t
				? limits.NormalizeStopPrice(entryPrice, t, limits.MinTakeProfitDistancePrice)
				: consoleTarget;

		return new RobotEntryPlan(capitalFraction, stopLoss, takeProfit);
	}

	/// <summary>
	/// Where the open position's stop should move to under breakeven and trailing, or null to leave it. A stop only
	/// ever tightens, and never to the far side of the current exit price, where it would close the trade at once.
	/// </summary>
	public static double? ManagedStopPrice(Position position, double bid, double ask, RobotPositionRules rules, double? atr, double priceStep)
	{
		if ((position.InitialStopLossPrice ?? position.StopLossPrice) is not { } initialStop || position.Size <= 0)
			return null;
		double risk = Math.Abs(position.EntryPrice - initialStop);
		if (risk <= 0)
			return null;

		int sign = position.Direction == PositionDirection.Long ? 1 : -1;
		double exitPrice = sign > 0 ? bid : ask;
		double movedR = sign * (exitPrice - position.EntryPrice) / risk;
		double? stop = position.StopLossPrice;

		void Consider(double candidate)
		{
			if (sign * (exitPrice - candidate) > 0 && (stop is null || sign * (candidate - stop.Value) > 0))
				stop = candidate;
		}

		if (rules.BreakevenEnabled && movedR >= rules.BreakevenAtR)
			Consider(position.EntryPrice);
		if (rules.TrailEnabled && movedR >= rules.TrailStartR && atr is > 0)
		{
			double bestExitPrice = position.EntryPrice + sign * position.PeakUnrealizedPnl / position.Size;
			Consider(bestExitPrice - sign * rules.TrailAtrMultiple * atr.Value);
		}

		if (stop is not { } moved)
			return null;
		double rounded = RiskHelper.RoundToStep(moved, priceStep);
		return rounded == position.StopLossPrice ? null : rounded;
	}

	// Structure falls back to ATR when no level is far enough away, and ATR falls back to the console when ATR is unknown.
	private static double? RuleStopPrice(
		RobotPositionRules rules,
		RobotMarketLevels levels,
		PositionDirection direction,
		double entryPrice,
		double pipSize
	)
	{
		if (rules.StopMode == RobotStopMode.Console)
			return null;

		int sign = direction == PositionDirection.Long ? -1 : 1;
		if (rules.StopMode == RobotStopMode.Structure && NearestLevel(levels, entryPrice, sign, rules.StructureMinAtr) is { } level)
			return level + sign * rules.StructureBufferPips * pipSize;

		return levels.Atr is > 0 ? entryPrice + sign * rules.StopAtrMultiple * levels.Atr.Value : null;
	}

	private static double? RuleTargetPrice(
		RobotPositionRules rules,
		RobotMarketLevels levels,
		PositionDirection direction,
		double entryPrice,
		double? stopLoss
	)
	{
		int sign = direction == PositionDirection.Long ? 1 : -1;
		if (rules.TargetMode == RobotTargetMode.Structure && NearestLevel(levels, entryPrice, sign, rules.StructureMinAtr) is { } level)
			return level;

		return stopLoss is { } stop && Math.Abs(entryPrice - stop) > 0
			? entryPrice + sign * rules.TargetR * Math.Abs(entryPrice - stop)
			: null;
	}

	/// <summary>The structure level nearest entry on one side (sign +1 above, -1 below), at least the minimum ATR distance away.</summary>
	private static double? NearestLevel(RobotMarketLevels levels, double entryPrice, int sign, double minAtr)
	{
		double minDistance = levels.Atr is > 0 ? minAtr * levels.Atr.Value : 0;
		var candidates = levels.Structure.Where(level => sign * (level - entryPrice) > minDistance).ToList();
		return candidates.Count == 0 ? null : candidates.MinBy(level => Math.Abs(level - entryPrice));
	}

	private static double? VolumeForStop(
		TradeConsoleOptions options,
		double entryPrice,
		double stopPrice,
		double balance,
		SymbolTradingLimits limits
	)
	{
		if (options.VolumeMode == VolumeSizingMode.Manual)
			return options.VolumeUnits;
		if (options.RiskPercent is not > 0)
			return null;
		return RiskHelper.SizeForRiskRounded(options.RiskPercent.Value / 100.0 * balance, entryPrice, stopPrice, limits.VolumeStep);
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
