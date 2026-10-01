namespace Poink.Sim.Models.TrainingModels
{
	public enum PositionSizingMode
	{
		FixedVolume,
		RiskPerTrade,
	}

	public enum StopLossUnit
	{
		Pips,
		BalancePercent,
	}

	/// <summary>
	/// The trade parameters the trader is allowed to use. Volume is in units of the instrument.
	/// StopLoss is a distance in <see cref="StopLossUnit"/> (0 = no stop); RiskPerTradePercent and
	/// TakeProfitBalancePercent are percents of balance (0 take-profit = no target).
	/// RiskPerTrade derives volume from the stop distance, so its stop is always in pips -- a
	/// balance-percent stop would make volume and stop depend on each other.
	/// </summary>
	public sealed record PositionControlOptionsModel(
		PositionSizingMode SizingMode = PositionSizingMode.FixedVolume,
		double Volume = 1,
		double RiskPerTradePercent = 1,
		double StopLoss = 0,
		StopLossUnit StopLossUnit = StopLossUnit.Pips,
		double TakeProfitBalancePercent = 0
	)
	{
		public bool HasStopLoss => StopLoss > 0;

		public bool HasTakeProfit => TakeProfitBalancePercent > 0;

		public StopLossUnit EffectiveStopLossUnit => SizingMode == PositionSizingMode.RiskPerTrade ? StopLossUnit.Pips : StopLossUnit;

		public string? Validate()
		{
			if (SizingMode == PositionSizingMode.RiskPerTrade && !HasStopLoss)
				return "Risk per trade sizing needs a stop-loss distance in pips above 0.";
			if (SizingMode == PositionSizingMode.FixedVolume && Volume <= 0)
				return "Volume must be above 0.";
			if (SizingMode == PositionSizingMode.RiskPerTrade && RiskPerTradePercent <= 0)
				return "Risk per trade must be above 0%.";
			if (StopLoss < 0 || TakeProfitBalancePercent < 0)
				return "Stop-loss and take-profit cannot be negative.";
			return null;
		}
	}

	/// <summary>One entry's size and risk at a given price, in account currency. Distances are price differences from the fill. MarginLimited is true when volume was cut to what the balance and leverage can carry.</summary>
	public readonly record struct PositionSizingPreview(
		double Volume,
		double Notional,
		double Margin,
		double LossAtStop,
		double ProfitAtTarget,
		double StopDistance,
		double TakeProfitDistance,
		double? RewardRisk,
		bool MarginLimited
	);
}
