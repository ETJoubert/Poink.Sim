using System.Globalization;
using Poink.Sim.Helpers;

namespace Poink.Sim.Repository;

public sealed class ModelTrainingParameters
{
	public int EpisodeCount { get; set; }
	public int EpisodeBars { get; set; }

	public int BarWidthSeconds { get; set; }

	public double StartingCash { get; set; }
	public double LearningRate { get; set; }

	public int? LookbackBars { get; set; }

	public double Leverage { get; set; } = 1.0;
	public double StopOutLevelPercent { get; set; } = 50.0;
	public double CommissionRatePercent { get; set; }

	public override string ToString() =>
		string.Create(
			CultureInfo.InvariantCulture,
			$"episodes={EpisodeCount} bars={EpisodeBars}@{(BarWidthSeconds > 0 ? BarSizeOptions.Label(BarWidthSeconds) : "?")} cash={StartingCash:N0} lr={LearningRate:G3}"
				+ $" lev={Leverage:G3}x commission={CommissionRatePercent:G3}%{(LookbackBars is { } lookback ? $" lookback={lookback}" : "")}"
		);
}
