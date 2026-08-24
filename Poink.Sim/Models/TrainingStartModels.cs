using System;
using Poink.Sim.Training;

namespace Poink.Sim.Models
{
    /// <summary>
    /// Parameters for starting a training run via <c>ITrainingService.TryStartTraining</c>.
    /// A null <see cref="HistoricRange"/> runs against synthetic (dummy) ticks instead of historic data.
    /// </summary>
    internal sealed record TrainingStartRequest(
        string BrainName,
        string StrategyName,
        int EpisodeCount,
        int EpisodeBars,
        int BarWidthSeconds,
        double StartingCash,
        double LearningRate,
        (string Table, DateTime From, DateTime To)? HistoricRange,
        Trader.TraderSettings? TraderSettings = null,
        bool PauseAfterFirstEpisode = false,
        EpisodeTimeConstraint? EpisodeTimeConstraint = null,
        SimulationSteppingMode SteppingMode = SimulationSteppingMode.Bar,
        double CellWidth = 0.10);
}
