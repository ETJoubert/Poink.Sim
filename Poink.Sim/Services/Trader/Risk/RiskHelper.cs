using System;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

/// <summary>
/// Stateless risk calculations shared by simulation and training.
/// Position sizing mirrors <see cref="Trader"/>: committed = fraction * cash,
/// notional = committed * leverage, size = notional / price. Commission is excluded.
/// </summary>
internal static class RiskHelper
{
	/// <summary>
	/// Money lost when a stop is hit for a position of <paramref name="size"/> units.
	/// Positive result is a loss; a negative result means the stop sits on the profitable side.
	/// </summary>
	public static double RiskOnStop(PositionDirection direction, double size, double entryPrice, double stopLossPrice)
	{
		double adverseMove =
			direction == PositionDirection.Long ? entryPrice - stopLossPrice : stopLossPrice - entryPrice;
		return size * adverseMove;
	}

	/// <summary>
	/// Units a capital-fraction entry produces under leverage at <paramref name="entryPrice"/>.
	/// </summary>
	public static double PositionSize(double cash, double capitalFraction, double leverage, double entryPrice)
	{
		if (entryPrice <= 0)
			return 0;
		double committed = Math.Clamp(capitalFraction, 0, 1) * cash;
		return committed * leverage / entryPrice;
	}

	/// <summary>
	/// Money lost when a stop is hit for a capital-fraction entry, evaluated before it opens.
	/// </summary>
	public static double ProjectedRiskOnStop(
		PositionDirection direction,
		double cash,
		double capitalFraction,
		double leverage,
		double entryPrice,
		double stopLossPrice
	)
	{
		double size = PositionSize(cash, capitalFraction, leverage, entryPrice);
		return RiskOnStop(direction, size, entryPrice, stopLossPrice);
	}

	/// <summary>
	/// Largest loss a stop-less capital-fraction entry can take before the margin stop-out force-closes it.
	/// The stop-out floor is reached when equity falls to marginUsed * stopOut%, mirroring <see cref="Trader"/>.
	/// A long is additionally capped by its notional, the loss when price reaches zero.
	/// </summary>
	public static double MaxRiskAtStopOut(
		PositionDirection direction,
		double cash,
		double capitalFraction,
		double leverage,
		double stopOutLevelPercent
	)
	{
		double marginUsed = Math.Clamp(capitalFraction, 0, 1) * cash;
		double stopOutFloorLoss = cash - marginUsed * stopOutLevelPercent / 100.0;
		if (direction != PositionDirection.Long)
			return stopOutFloorLoss;
		double notional = marginUsed * leverage;
		return Math.Min(notional, stopOutFloorLoss);
	}

	/// <summary>
	/// Units needed so a stop hit costs exactly <paramref name="riskAmount"/>.
	/// Returns 0 when entry and stop coincide, giving no distance to size against.
	/// </summary>
	public static double SizeForRisk(double riskAmount, double entryPrice, double stopLossPrice)
	{
		double distance = Math.Abs(entryPrice - stopLossPrice);
		return distance > 0 ? riskAmount / distance : 0;
	}

	/// <summary>
	/// Capital fraction needed so a stop hit costs exactly <paramref name="riskAmount"/>, under leverage.
	/// Returns 0 when the inputs give no basis to size against.
	/// </summary>
	public static double CapitalFractionForRisk(
		double riskAmount,
		double cash,
		double leverage,
		double entryPrice,
		double stopLossPrice
	)
	{
		if (cash <= 0 || leverage <= 0)
			return 0;
		double size = SizeForRisk(riskAmount, entryPrice, stopLossPrice);
		double committed = size * entryPrice / leverage;
		return committed / cash;
	}

	/// <summary>
	/// Absolute stop-loss price a pip distance places, on the losing side of the entry:
	/// below entry for a long, above entry for a short.
	/// </summary>
	public static double StopLossPriceFromPips(
		PositionDirection direction,
		double entryPrice,
		double pips,
		double pipSize
	) => entryPrice + (direction == PositionDirection.Long ? -1 : 1) * pips * pipSize;

	/// <summary>
	/// Absolute stop-loss price whose hit costs exactly <paramref name="riskAmount"/> for a capital-fraction
	/// entry under leverage. Returns null when the entry produces no size to distribute the risk over.
	/// </summary>
	public static double? StopLossPriceForRisk(
		PositionDirection direction,
		double cash,
		double capitalFraction,
		double leverage,
		double entryPrice,
		double riskAmount
	) => StopLossPriceForSizeRisk(direction, PositionSize(cash, capitalFraction, leverage, entryPrice), entryPrice, riskAmount);

	/// <summary>
	/// Absolute stop-loss price whose hit costs exactly <paramref name="riskAmount"/> for a known position
	/// size. Returns null when the size gives no basis to distribute the risk over.
	/// </summary>
	public static double? StopLossPriceForSizeRisk(
		PositionDirection direction,
		double size,
		double entryPrice,
		double riskAmount
	)
	{
		if (size <= 0)
			return null;
		double adverseMove = riskAmount / size;
		return entryPrice + (direction == PositionDirection.Long ? -1 : 1) * adverseMove;
	}

	/// <summary>
	/// Absolute take-profit price a pip distance places, on the winning side of the entry:
	/// above entry for a long, below entry for a short.
	/// </summary>
	public static double TakeProfitPriceFromPips(
		PositionDirection direction,
		double entryPrice,
		double pips,
		double pipSize
	) => entryPrice + (direction == PositionDirection.Long ? 1 : -1) * pips * pipSize;

	/// <summary>
	/// Absolute take-profit price whose hit gains exactly <paramref name="rewardAmount"/> for a capital-fraction
	/// entry under leverage. Returns null when the entry produces no size to distribute the reward over.
	/// </summary>
	public static double? TakeProfitPriceForReward(
		PositionDirection direction,
		double cash,
		double capitalFraction,
		double leverage,
		double entryPrice,
		double rewardAmount
	) => TakeProfitPriceForSizeReward(direction, PositionSize(cash, capitalFraction, leverage, entryPrice), entryPrice, rewardAmount);

	/// <summary>
	/// Absolute take-profit price whose hit gains exactly <paramref name="rewardAmount"/> for a known position
	/// size. Returns null when the size gives no basis to distribute the reward over.
	/// </summary>
	public static double? TakeProfitPriceForSizeReward(
		PositionDirection direction,
		double size,
		double entryPrice,
		double rewardAmount
	)
	{
		if (size <= 0)
			return null;
		double favorableMove = rewardAmount / size;
		return entryPrice + (direction == PositionDirection.Long ? 1 : -1) * favorableMove;
	}

	/// <summary>
	/// Risk as a fraction of equity. Returns 0 when equity is non-positive.
	/// </summary>
	public static double RiskFraction(double riskAmount, double equity) => equity > 0 ? riskAmount / equity : 0;

	/// <summary>
	/// Money lost when this open position's stop is hit. Returns 0 when no stop is set.
	/// </summary>
	public static double RiskOnStop(this Position position) =>
		position.StopLossPrice is { } stop
			? RiskOnStop(position.Direction, position.Size, position.EntryPrice, stop)
			: 0;
}
