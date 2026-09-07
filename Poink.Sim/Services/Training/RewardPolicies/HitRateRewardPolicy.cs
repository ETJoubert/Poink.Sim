namespace Poink.Sim.Training;

internal sealed class HitRateRewardPolicy : IRewardPolicy
{
	public string Name => "Hit Rate";

	public double ComputeReward(RewardContext context)
	{
		var delta = context.CapitalNow.Equity - context.PreviousCapital.Equity;
		return delta switch
		{
			> 0 => 1d,
			< 0 => -1d,
			_ => 0d
		};
	}
}
