namespace Poink.Sim.Models
{
	/// <summary>
	/// Parameters for persisting a brain's current weights via <c>ITrainingService.SaveBrain</c>.
	/// </summary>
	internal sealed record TrainingSaveRequest(
		string SavedName,
		string BrainName,
		string StrategyName,
		string Dataset,
		int EpisodeBars,
		int BarWidthSeconds,
		double StartingCash,
		double LearningRate,
		Trader.TraderSettings? TraderSettings = null
	);
}
