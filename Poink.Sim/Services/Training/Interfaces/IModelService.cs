
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal interface IModelService
{
    IReadOnlyList<NeuralWeightsSummary> Models { get; }
    string? Status { get; }

    event Action? OnChanged;

    bool SaveModel(
        string name, string brainName, string strategyName, string rewardPolicyName, string dataset,
        ModelTrainingParameters parameters, BrainRecipe? recipe, DateTime? trainedAt,
        long weightCount, double avgReward, byte[] weights);

    byte[]? LoadWeights(Guid id);
    bool DeleteModel(Guid id);
    void Refresh();
}
