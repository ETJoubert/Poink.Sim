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
	/// Money gained when a target is hit for a position of <paramref name="size"/> units.
	/// Positive result is a gain; a negative result means the target sits on the losing side.
	/// </summary>
	public static double RewardOnTarget(PositionDirection direction, double size, double entryPrice, double targetPrice) =>
		-RiskOnStop(direction, size, entryPrice, targetPrice);

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
	/// Margin committed for a position of <paramref name="size"/> units at <paramref name="entryPrice"/>,
	/// under leverage. Returns 0 when leverage is non-positive.
	/// </summary>
	public static double MarginRequired(double size, double entryPrice, double leverage) =>
		leverage > 0 ? size * entryPrice / leverage : 0;

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
	/// Units needed so a stop hit costs exactly <paramref name="riskAmount"/>, rounded to the nearest
	/// <paramref name="volumeStep"/>. Returns null when the inputs give no basis to size against, or the
	/// rounded result is zero.
	/// </summary>
	public static double? SizeForRiskRounded(
		double riskAmount,
		double entryPrice,
		double stopLossPrice,
		double volumeStep
	)
	{
		double rawSize = SizeForRisk(riskAmount, entryPrice, stopLossPrice);
		if (rawSize <= 0)
			return null;
		double rounded = RoundToStep(rawSize, volumeStep);
		return rounded > 0 ? rounded : null;
	}

	/// <summary>
	/// Nearest multiple of <paramref name="step"/> to <paramref name="value"/>, cleaned of floating-point
	/// residue. Returns <paramref name="value"/> unchanged when the step is non-positive.
	/// </summary>
	public static double RoundToStep(double value, double step) =>
		step > 0 ? Math.Round(Math.Round(value / step) * step, 8) : value;

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
	/// Capital fraction needed so a stop hit costs exactly <paramref name="riskAmount"/>, under leverage.
	/// Null when the inputs give no basis to size against, or the required fraction exceeds 100% of cash.
	/// </summary>
	public static double? CapitalFractionForRiskBounded(
		double riskAmount,
		double cash,
		double leverage,
		double entryPrice,
		double stopLossPrice
	)
	{
		double fraction = CapitalFractionForRisk(riskAmount, cash, leverage, entryPrice, stopLossPrice);
		return fraction is > 0 and <= 1 ? fraction : null;
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
	/// Reward divided by risk. Null when either side is non-positive, since the ratio is undefined
	/// (no risk to divide by) or meaningless (the trade isn't a risk/reward pair).
	/// </summary>
	public static double? RewardToRiskRatio(double risk, double reward) => risk > 0 && reward > 0 ? reward / risk : null;

	/// <summary>
	/// Distance between two prices, expressed in pips.
	/// </summary>
	public static double PriceDistanceInPips(double priceA, double priceB, double pipSize) =>
		Math.Abs(priceA - priceB) / pipSize;

	/// <summary>
	/// Money lost when this open position's stop is hit. Returns 0 when no stop is set.
	/// </summary>
	public static double RiskOnStop(this Position position) =>
		position.StopLossPrice is { } stop
			? RiskOnStop(position.Direction, position.Size, position.EntryPrice, stop)
			: 0;
}
