
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal interface INeuralWeightsService
{
    Task<Guid> CreateAsync(
        Guid neuralNetworkId, string name, string strategyName, string rewardPolicyName, string dataset,
        string parametersJson, string? structureSnapshotJson, byte[] data, long weightCount, double avgReward,
        DateTime? trainedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeuralWeightsModel>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeuralWeightsModel>> ListByNetworkAsync(Guid neuralNetworkId, CancellationToken cancellationToken = default);

    Task<NeuralWeightsModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid id, string name, string strategyName, string rewardPolicyName, string dataset,
        string parametersJson, string? structureSnapshotJson, byte[] data, long weightCount, double avgReward,
        DateTime? trainedAt, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
