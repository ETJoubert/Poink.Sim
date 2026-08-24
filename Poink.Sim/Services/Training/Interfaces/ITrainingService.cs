
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal interface ITrainingService
{
    IReadOnlyList<string> StrategyNames { get; }

    bool IsRunning { get; }
    bool IsPaused { get; }
    string? Error { get; }
    int EpisodesCompleted { get; }
    int TotalEpisodes { get; }
    double RecentAverageReward { get; }
    double BestReward { get; }
    double WorstReward { get; }
    double EpisodesPerSecond { get; }
    RlActionCounts ActionCounts { get; }

    IReadOnlyList<RlInputFeature>? LatestInputs { get; }
    IReadOnlyList<RlOutputFeature>? LatestOutputs { get; }
    NeatTopologySnapshot? LatestNeatTopology { get; }
    IReadOnlyList<HlocAvdtBar> LatestEpisodeBars { get; }
    IReadOnlyList<BarPriceHistogram> LatestEpisodeBarHistograms { get; }
    int LatestEpisodeWarmupBars { get; }
    SimulationSteppingMode LatestEpisodeSteppingMode { get; }
    double LatestEpisodeCellWidth { get; }
    IReadOnlyList<Trade> LatestEpisodeTrades { get; }
    IReadOnlyList<IReadOnlyList<RlInputFeature>> LatestEpisodeBarInputs { get; }
    IReadOnlyList<double> LatestEpisodeBarEquity { get; }
    IReadOnlyList<IReadOnlyList<RlOutputFeature>> LatestEpisodeBarOutputs { get; }
    IReadOnlyList<RlBarAction> LatestEpisodeBarActions { get; }

    IReadOnlyList<RlEpisodeMetric> RecentEpisodes { get; }
    IReadOnlyList<NeatGenerationStats> RecentGenerations { get; }
    IReadOnlyList<RlEpisodeTrades> RecentEpisodeTrades { get; }
    IReadOnlyList<RlEpisodeWindow> RecentEpisodeWindows { get; }
    string? SaveStatus { get; }
    IReadOnlyList<RlTrainingLogEntry> RecentLog { get; }

    event Action? OnChanged;

    NeuralWeightsSummary? GetActiveSnapshot(string brainName);

    bool TryStartTraining(TrainingStartRequest request);

    bool ResetRun();
    void PauseTraining();
    void ResumeTraining();
    void StepTraining();
    void StopTraining();

    bool SaveTrainedBrain(TrainingSaveRequest request);

    bool LoadSavedBrain(Guid id);
}
