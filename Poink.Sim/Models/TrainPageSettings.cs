using System;
using System.Collections.Generic;

namespace Poink.Sim.Models
{
    /// <summary>
    /// Snapshot of the Train page's Run Setup config (Data Range/Brain/Strategy/Episode
    /// Timing/Trader), persisted to browser localStorage so it survives navigating away and
    /// back -- IRlTrainingService keeps a run going across navigation, but the page's own
    /// fields are reset to their defaults every time Blazor recreates the component.
    /// </summary>
    internal sealed record TrainPageSettings(
        string Table,
        DateTime From,
        DateTime To,
        string BrainName,
        string StrategyName,
        double LearningRate,
        int BarWidthSeconds,
        SimulationSteppingMode SteppingMode,
        double CellWidth,
        int EpisodeCount,
        int EpisodeBars,
        bool RestrictEpisodeTiming,
        List<DayOfWeek> AllowedWeekdays,
        TimeSpan SessionStart,
        TimeSpan SessionEnd,
        double StartingCash,
        double Leverage,
        double CommissionRatePercent,
        double StopOutLevelPercent,
        List<string> SelectedRiskPolicies);
}
