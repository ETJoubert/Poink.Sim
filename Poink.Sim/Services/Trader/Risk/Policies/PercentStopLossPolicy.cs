using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk.Policies;

internal sealed class PercentStopLossPolicy : IRiskPolicy
{
	public const string PolicyName = "2% stop-loss";
	public const double DefaultStopFraction = 0.02;

	private readonly double _stopFraction;

	public PercentStopLossPolicy(double stopFraction = DefaultStopFraction) => _stopFraction = stopFraction;

	public string Name => PolicyName;

	public string Description =>
		"Attaches a stop-loss risking 2% of cash to any entry that has none. Leaves an entry's existing stop-loss alone.";

	public string? Status => null;

	public IRiskPolicy Fresh() => new PercentStopLossPolicy(_stopFraction);

	public RiskReview Review(SimulationDecision proposed, RiskContext context)
	{
		if (proposed.Action is not (SimulationAction.EnterLong or SimulationAction.EnterShort))
			return RiskReview.Permit(proposed);
		if (proposed.StopLossPrice is not null)
			return RiskReview.Permit(proposed);

		var direction =
			proposed.Action == SimulationAction.EnterLong ? PositionDirection.Long : PositionDirection.Short;
		double entryPrice = proposed.TriggerPrice ?? (direction == PositionDirection.Long ? context.Ask : context.Bid);
		if (entryPrice <= 0)
			return RiskReview.Permit(proposed);

		double riskAmount = _stopFraction * context.Cash;
		if (
			RiskHelper.StopLossPriceForRisk(
				direction,
				context.Cash,
				proposed.CapitalFraction,
				context.Leverage,
				entryPrice,
				riskAmount
			)
			is not { } stopPrice
		)
			return RiskReview.Permit(proposed);

		return RiskReview.Modify(
			proposed with
			{
				StopLossPrice = stopPrice,
			},
			$"attached a stop-loss risking {_stopFraction:P0} of cash"
		);
	}

	public IReadOnlyList<SimulationDecision> Supervise(RiskContext context) => Array.Empty<SimulationDecision>();
}
