
using System;
using System.Collections.Generic;
using System.Globalization;
using Poink.Sim.Brain.Neural;
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
        string.Create(CultureInfo.InvariantCulture,
            $"episodes={EpisodeCount} bars={EpisodeBars}@{(BarWidthSeconds > 0 ? BarSizeOptions.Label(BarWidthSeconds) : "?")} cash={StartingCash:N0} lr={LearningRate:G3}" +
            $" lev={Leverage:G3}x commission={CommissionRatePercent:G3}%{(LookbackBars is { } lookback ? $" lookback={lookback}" : "")}");
}


public sealed record TrainedModelSummary(
    long Id, string Name, string BrainName, string StrategyName, string RewardPolicyName, string Dataset,
    ModelTrainingParameters Parameters, BrainRecipe? Recipe, DateTime? TrainedAt,
    long WeightCount, double AvgReward, DateTime SavedAt);

internal interface IModelRepository
{
    long SaveModel(
        string name, string brainName, string strategyName, string rewardPolicyName, string dataset,
        ModelTrainingParameters parameters, BrainRecipe? recipe, DateTime? trainedAt,
        long weightCount, double avgReward, byte[] weights);

    List<TrainedModelSummary> ListModels();

    byte[]? LoadWeights(long id);

    void DeleteModel(long id);
}
