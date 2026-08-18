
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal interface IRlTrainingService
{
    IReadOnlyList<string> BrainNames { get; }
    IReadOnlyList<string> DraftBrainNames { get; }
    IReadOnlyList<BrainOption> BrainOptions { get; }
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
    IReadOnlyList<RlEpisodeTrades> RecentEpisodeTrades { get; }
    IReadOnlyList<RlEpisodeWindow> RecentEpisodeWindows { get; }
    string? SaveStatus { get; }
    IReadOnlyList<RlTrainingLogEntry> RecentLog { get; }

    event Action? OnChanged;

    TrainedModelSummary? GetActiveSnapshot(string brainName);
    BrainRecipe? GetRecipe(string brainName);
    IReadOnlyList<NetworkLayerShape> DescribeNetworkShape(string brainName);
    int GetWarmupBars(string brainName);

    bool TryStartTraining(
        string brainName, string strategyName, int episodeCount, int episodeBars, int barWidthSeconds, double startingCash,
        double learningRate, (string Table, DateTime From, DateTime To)? historicRange,
        Trader.TraderSettings? traderSettings = null, bool pauseAfterFirstEpisode = false,
        EpisodeTimeConstraint? episodeTimeConstraint = null,
        SimulationSteppingMode steppingMode = SimulationSteppingMode.Bar, double cellWidth = 0.10);

    bool ResetRun();
    void PauseTraining();
    void ResumeTraining();
    void StepTraining();
    void StopTraining();

    bool TryBuildDraftBrain(
        string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes,
        IReadOnlyList<ActionOutputKind>? actionOutputKinds = null, IReadOnlyList<SimulationAction>? enabledDirections = null,
        EncoderSizeOptions? encoderSizes = null, DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
        double decisionTemperature = 1.0, CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow);

    bool TryBuildBrain(
        string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes,
        IReadOnlyList<ActionOutputKind>? actionOutputKinds = null, IReadOnlyList<SimulationAction>? enabledDirections = null,
        EncoderSizeOptions? encoderSizes = null, DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
        double decisionTemperature = 1.0, CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow);

    bool SaveBrain(
        string savedName, string brainName, string strategyName, string dataset,
        int episodeBars, int barWidthSeconds, double startingCash, double learningRate,
        Trader.TraderSettings? traderSettings = null);

    bool LoadSavedBrain(long id);
}
