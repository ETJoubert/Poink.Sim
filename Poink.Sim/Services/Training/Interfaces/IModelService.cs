
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal interface IModelService
{
    IReadOnlyList<TrainedModelSummary> Models { get; }
    string? Status { get; }

    event Action? OnChanged;

    bool SaveModel(
        string name, string brainName, string strategyName, string rewardPolicyName, string dataset,
        ModelTrainingParameters parameters, BrainRecipe? recipe, DateTime? trainedAt,
        long weightCount, double avgReward, byte[] weights);

    byte[]? LoadWeights(long id);
    bool DeleteModel(long id);
    void Refresh();
}
