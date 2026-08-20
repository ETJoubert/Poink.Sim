using System;
using System.Collections.Generic;
using Poink.Sim.Training;

namespace Poink.Sim.Models
{
    /// <summary>
    /// Snapshot of a training run's Run Setup config (Data Range/Brain/Strategy/Episode
    /// Timing/Trader), taken at save time -- decoupled from TrainPageSettings, which
    /// persists the page's live UI state rather than a saved result.
    /// </summary>
    internal sealed record TrainingConfig(
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
        IReadOnlyList<DayOfWeek> AllowedWeekdays,
        TimeSpan SessionStart,
        TimeSpan SessionEnd,
        double StartingCash,
        double Leverage,
        double CommissionRatePercent,
        double StopOutLevelPercent,
        IReadOnlyList<string> SelectedRiskPolicies);

    /// <summary>
    /// Training-signal stats for a run (reward/episode-throughput). Excludes trade/trader
    /// outcomes -- see TradeStatistics for those.
    /// </summary>
    internal sealed record TrainingSessionStats(
        int EpisodesCompleted,
        int TotalEpisodes,
        double RecentAverageReward,
        double BestReward,
        double WorstReward,
        double EpisodesPerSecond,
        RlActionCounts ActionCounts,
        double WinRatePercent,
        double RewardSpread,
        double RewardStdDev,
        double RewardMedian);

    /// <summary>
    /// Latest-snapshot series backing the reward/trend/coverage charts.
    /// </summary>
    internal sealed record TrainingResultsGraph(
        IReadOnlyList<RlEpisodeMetric> Episodes,
        IReadOnlyList<RlEpisodeWindow> EpisodeWindows);

    internal sealed record TrainingResultModel(
        Guid Id,
        string Name,
        TrainingConfig Config,
        TrainingSessionStats Stats,
        TrainingResultsGraph Graph,
        DateTime CreatedAt);

    internal sealed record TrainingResultSummary(
        Guid Id,
        string Name,
        string BrainName,
        string StrategyName,
        DateTime CreatedAt,
        int EpisodesCompleted,
        int TotalEpisodes,
        double RecentAverageReward,
        double BestReward);
}
