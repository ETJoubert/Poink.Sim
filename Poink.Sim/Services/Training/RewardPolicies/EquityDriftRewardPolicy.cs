namespace Poink.Sim.Training;

internal sealed class EquityDriftRewardPolicy : IRewardPolicy
{
	public string Name => "Equity Drift (default)";

	public double ComputeReward(RewardContext context) => context.CapitalNow.Equity - context.PreviousCapital.Equity;
}
