using System;

namespace Poink.Sim.Models
{
    internal sealed record NeuralWeightsModel(
        Guid Id,
        Guid NeuralNetworkId,
        string Name,
        string StrategyName,
        string RewardPolicyName,
        string Dataset,
        string ParametersJson,
        string? StructureSnapshotJson,
        byte[] Data,
        long WeightCount,
        double AvgReward,
        DateTime? TrainedAt,
        DateTime CreatedAt
    );
}

