namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>The Playback tab's default speed and which panels are shown during replay.</summary>
	internal sealed record PlaybackOptionsModel(
		int PlaybackSpeedIndex = 2,
		bool ShowMarketChart = true,
		bool ShowXGrid = true,
		bool ShowTxFold = true,
		bool ShowEquityCurve = true,
		bool ShowFeatures = true
	);
}
